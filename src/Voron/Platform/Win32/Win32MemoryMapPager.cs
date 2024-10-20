using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Sparrow;
using Sparrow.Logging;
using Sparrow.Platform;
using Sparrow.Utils;
using Voron.Data.BTrees;
using Voron.Global;
using Voron.Impl;
using Voron.Impl.Paging;
using Voron.Util;
using static Voron.Platform.Win32.Win32NativeMethods;

namespace Voron.Platform.Win32
{
    public unsafe class Win32MemoryMapPager : AbstractPager
    {
        public readonly long AllocationGranularity;
        private long _totalAllocationSize;
        private readonly FileInfo _fileInfo;
        private readonly FileStream _fileStream;
        private readonly SafeFileHandle _handle;
        private readonly Win32NativeFileAccess _access;
        private readonly MemoryMappedFileAccess _memoryMappedFileAccess;
        private bool _copyOnWriteMode;
        private readonly Logger _logger;

        [StructLayout(LayoutKind.Explicit)]
        public struct SplitValue
        {
            [FieldOffset(0)]
            public ulong Value;

            [FieldOffset(0)]
            public uint Low;

            [FieldOffset(4)]
            public uint High;
        }

        public Win32MemoryMapPager(StorageEnvironmentOptions options,string file,
            long? initialFileSize = null,
            Win32NativeFileAttributes fileAttributes = Win32NativeFileAttributes.Normal,
            Win32NativeFileAccess access = Win32NativeFileAccess.GenericRead | Win32NativeFileAccess.GenericWrite,
            bool usePageProtection = false)
            : base(options, usePageProtection)
        {
            SYSTEM_INFO systemInfo;
            GetSystemInfo(out systemInfo);
            FileName = file;
            _logger = LoggingSource.Instance.GetLogger<StorageEnvironment>($"Pager-{file}");
            AllocationGranularity = systemInfo.allocationGranularity;
            _access = access;
            _copyOnWriteMode = Options.CopyOnWriteMode && FileName.EndsWith(Constants.DatabaseFilename);
            if (_copyOnWriteMode)
            {
                _memoryMappedFileAccess = MemoryMappedFileAccess.Read | MemoryMappedFileAccess.CopyOnWrite;
                fileAttributes = Win32NativeFileAttributes.Readonly;
                _access = Win32NativeFileAccess.GenericRead;
            }
            else
            {
                _memoryMappedFileAccess = _access == Win32NativeFileAccess.GenericRead
                ? MemoryMappedFileAccess.Read
                : MemoryMappedFileAccess.ReadWrite;
            }

            _handle = Win32NativeFileMethods.CreateFile(file, access,
                                                        Win32NativeFileShare.Read | Win32NativeFileShare.Write | Win32NativeFileShare.Delete, IntPtr.Zero,
                                                        Win32NativeFileCreationDisposition.OpenAlways, fileAttributes, IntPtr.Zero);
            if (_handle.IsInvalid)
            {
                int lastWin32ErrorCode = Marshal.GetLastWin32Error();
                throw new IOException("Failed to open file storage of Win32MemoryMapPager for " + file,
                    new Win32Exception(lastWin32ErrorCode));
            }

            _fileInfo = new FileInfo(file);
            var drive = _fileInfo.Directory.Root.Name.TrimEnd('\\');

            try
            {
                if (PhysicalDrivePerMountCache.TryGetValue(drive, out UniquePhysicalDriveId) == false)
                    UniquePhysicalDriveId = GetPhysicalDriveId(drive);

                if (_logger.IsInfoEnabled)
                    _logger.Info($"Physical drive '{drive}' unique id = '{UniquePhysicalDriveId}' for file '{file}'");
            }
            catch (Exception ex)
            {
                UniquePhysicalDriveId = 0;
                if (_logger.IsInfoEnabled)
                    _logger.Info($"Failed to determine physical drive Id for drive letter '{drive}', file='{file}'", ex);
            }

            var streamAccessType = _access == Win32NativeFileAccess.GenericRead
                ? FileAccess.Read
                : FileAccess.ReadWrite;
            _fileStream = new FileStream(_handle, streamAccessType);

            _totalAllocationSize = _fileInfo.Length;

            if (_access.HasFlag(Win32NativeFileAccess.GenericWrite) ||
                _access.HasFlag(Win32NativeFileAccess.GenericAll) ||
                _access.HasFlag(Win32NativeFileAccess.FILE_GENERIC_WRITE))
            {
                var fileLength = _fileStream.Length;
                if (fileLength == 0 && initialFileSize.HasValue)
                    fileLength = initialFileSize.Value;

                if (_fileStream.Length == 0 || (fileLength % AllocationGranularity != 0))
                {
                    fileLength = NearestSizeToAllocationGranularity(fileLength);

                    Win32NativeFileMethods.SetFileLength(_handle, fileLength);
                }

                _totalAllocationSize = fileLength;
            }

            NumberOfAllocatedPages = _totalAllocationSize / PageSize;
            SetPagerState(CreatePagerState());
        }

        private uint GetPhysicalDriveId(string drive)
        {
            var sdn = new StorageDeviceNumber();

            var driveHandle = CreateFile(@"\\.\" + drive, 0, 0, IntPtr.Zero, (uint)CreationDisposition.OPEN_EXISTING, 0, IntPtr.Zero);

            if (driveHandle.ToInt64() == -1)
            {
                int lastWin32ErrorCode = Marshal.GetLastWin32Error();
                throw new IOException("Failed to CreateFile for Drive : " + drive,
                    new Win32Exception(lastWin32ErrorCode));
            }
            try
            {
                int requiredSize;
                if (DeviceIoControl(driveHandle,
                        (int)IoControlCode.IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, new IntPtr(&sdn), sizeof(StorageDeviceNumber),
                        out requiredSize, IntPtr.Zero) == false)
                {
                    int lastWin32ErrorCode = Marshal.GetLastWin32Error();
                    throw new IOException("Failed to DeviceIoControl for Drive : " + drive,
                        new Win32Exception(lastWin32ErrorCode));
                }
            }
            finally
            {
                CloseHandle(driveHandle);
            }
            return (uint)((int)sdn.DeviceType << 8) + sdn.DeviceNumber;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long NearestSizeToAllocationGranularity(long size)
        {
            var modulos = size % AllocationGranularity;
            if (modulos == 0)
                return Math.Max(size, AllocationGranularity);

            return ((size / AllocationGranularity) + 1) * AllocationGranularity;
        }

        protected override PagerState AllocateMorePages(long newLength)
        {
            if (Disposed)
                ThrowAlreadyDisposedException();

            var newLengthAfterAdjustment = NearestSizeToAllocationGranularity(newLength);

            if (newLengthAfterAdjustment <= _totalAllocationSize)
                return null;

            var allocationSize = newLengthAfterAdjustment - _totalAllocationSize;

            Win32NativeFileMethods.SetFileLength(_handle, _totalAllocationSize + allocationSize);
            PagerState newPagerState = null;

#if VALIDATE
            // If we're on validate more, we don't want to allocate continuous pages because this
            // introduces weird conditions on the protection and unprotection routines (we have to
            // track boundaries, which is more complex than we're willing to do)
            newPagerState = CreatePagerState();

            SetPagerState(newPagerState);

            PagerState.DebugVerify(newLengthAfterAdjustment);
#else
            if (TryAllocateMoreContinuousPages(allocationSize) == false)
            {
                newPagerState = CreatePagerState();

                SetPagerState(newPagerState);

                PagerState.DebugVerify(newLengthAfterAdjustment);
            }
#endif

            _totalAllocationSize += allocationSize;
            NumberOfAllocatedPages = _totalAllocationSize / PageSize;

            return newPagerState;
        }

        private bool TryAllocateMoreContinuousPages(long allocationSize)
        {
            Debug.Assert(PagerState != null);
            Debug.Assert(PagerState.AllocationInfos != null);
            Debug.Assert(PagerState.Files != null && PagerState.Files.Any());

            var allocationInfo = RemapViewOfFileAtAddress(allocationSize, (ulong)_totalAllocationSize, PagerState.MapBase + _totalAllocationSize);

            if (allocationInfo == null)
                return false;

            PagerState.Files = PagerState.Files.Concat(allocationInfo.MappedFile);
            PagerState.AllocationInfos = PagerState.AllocationInfos.Concat(allocationInfo);

            if (PlatformDetails.CanPrefetch)
            {
                // We are asking to allocate pages. It is a good idea that they should be already in memory to only cause a single page fault (as they are continuous).
                Win32MemoryMapNativeMethods.WIN32_MEMORY_RANGE_ENTRY entry;
                entry.VirtualAddress = allocationInfo.BaseAddress;
                entry.NumberOfBytes = (IntPtr)allocationInfo.Size;

                Win32MemoryMapNativeMethods.PrefetchVirtualMemory(Win32Helper.CurrentProcess, (UIntPtr)1, &entry, 0);
            }
            return true;
        }

        private PagerState.AllocationInfo RemapViewOfFileAtAddress(long allocationSize, ulong offsetInFile, byte* baseAddress)
        {
            var offset = new SplitValue { Value = offsetInFile };

            var mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, _fileStream.Length,
                _memoryMappedFileAccess,
                 HandleInheritability.None, true);
            Win32MemoryMapNativeMethods.NativeFileMapAccessType mmfAccessType = _copyOnWriteMode
                ? Win32MemoryMapNativeMethods.NativeFileMapAccessType.Copy 
                : Win32MemoryMapNativeMethods.NativeFileMapAccessType.Read |
                  Win32MemoryMapNativeMethods.NativeFileMapAccessType.Write;
            var newMappingBaseAddress = Win32MemoryMapNativeMethods.MapViewOfFileEx(mmf.SafeMemoryMappedFileHandle.DangerousGetHandle(),
                mmfAccessType,
                offset.High, offset.Low,
                new UIntPtr((ulong)allocationSize),
                baseAddress);

            var hasMappingSucceeded = newMappingBaseAddress != null && newMappingBaseAddress != (byte*)0;
            if (!hasMappingSucceeded)
            {
                mmf.Dispose();
                return null;
            }

            ProtectPageRange(newMappingBaseAddress, (ulong)allocationSize);

            NativeMemory.RegisterFileMapping(_fileInfo.FullName, new IntPtr(newMappingBaseAddress), allocationSize);

            return new PagerState.AllocationInfo
            {
                BaseAddress = newMappingBaseAddress,
                Size = allocationSize,
                MappedFile = mmf
            };
        }

        private PagerState CreatePagerState()
        {
            var mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, _fileStream.Length,
                _memoryMappedFileAccess,
                HandleInheritability.None, true);

            var fileMappingHandle = mmf.SafeMemoryMappedFileHandle.DangerousGetHandle();
            Win32MemoryMapNativeMethods.NativeFileMapAccessType mmFileAccessType;
            if (_copyOnWriteMode)
            {
                mmFileAccessType =  Win32MemoryMapNativeMethods.NativeFileMapAccessType.Copy;
            }
            else
            {
                mmFileAccessType = _access == Win32NativeFileAccess.GenericRead
                    ? Win32MemoryMapNativeMethods.NativeFileMapAccessType.Read
                    : Win32MemoryMapNativeMethods.NativeFileMapAccessType.Read |
                      Win32MemoryMapNativeMethods.NativeFileMapAccessType.Write;
            }
            var startingBaseAddressPtr = Win32MemoryMapNativeMethods.MapViewOfFileEx(fileMappingHandle,
                mmFileAccessType,
                0, 0,
                UIntPtr.Zero, //map all what was "reserved" in CreateFileMapping on previous row
                null);


            if (startingBaseAddressPtr == (byte*)0) //system didn't succeed in mapping the address where we wanted
            {
                var innerException = new Win32Exception();

                var errorMessage = string.Format(
                    "Unable to allocate more pages - unsuccessfully tried to allocate continuous block of virtual memory with size = {0:##,###;;0} bytes",
                    (_fileStream.Length));

                throw new OutOfMemoryException(errorMessage, innerException);
            }

            NativeMemory.RegisterFileMapping(_fileInfo.FullName, new IntPtr(startingBaseAddressPtr), _fileStream.Length);

            // If we are working on memory validation mode, then protect the pages by default.
            ProtectPageRange(startingBaseAddressPtr, (ulong)_fileStream.Length);

            var allocationInfo = new PagerState.AllocationInfo
            {
                BaseAddress = startingBaseAddressPtr,
                Size = _fileStream.Length,
                MappedFile = mmf
            };

            var newPager = new PagerState(this)
            {
                Files = new[] { mmf },
                MapBase = startingBaseAddressPtr,
                AllocationInfos = new[] { allocationInfo }
            };

            return newPager;
        }

        protected override string GetSourceName()
        {
            if (_fileInfo == null)
                return "Unknown";
            return "MemMap: " + _fileInfo.FullName;
        }

        public override void Sync()
        {
            if (Disposed)
                ThrowAlreadyDisposedException();

            var currentState = GetPagerStateAndAddRefAtomically();
            try
            {
                using (var metric = Options.IoMetrics.MeterIoRate(FileName, IoMetrics.MeterType.DataSync, 0))
                {
                    foreach (var allocationInfo in currentState.AllocationInfos)
                    {
                        metric.IncrementSize(allocationInfo.Size);
                        if (
                            Win32MemoryMapNativeMethods.FlushViewOfFile(allocationInfo.BaseAddress,
                                new IntPtr(allocationInfo.Size)) == false)
                            throw new Win32Exception();
                    }

                    if (Win32MemoryMapNativeMethods.FlushFileBuffers(_handle) == false)
                        throw new Win32Exception();
                }
            }
            finally
            {
                currentState.Release();
            }
        }


        public override string ToString()
        {
            return _fileInfo.Name;
        }

        public override void Dispose()
        {
            if (Disposed)
                return;

            base.Dispose();

            _fileStream?.Dispose();
            _handle?.Dispose();
            if (DeleteOnClose)
                _fileInfo?.Delete();

        }

        public override void ReleaseAllocationInfo(byte* baseAddress, long size)
        {
            if (Win32MemoryMapNativeMethods.UnmapViewOfFile(baseAddress) == false)
                throw new Win32Exception();
            NativeMemory.UnregisterFileMapping(_fileInfo.FullName, new IntPtr(baseAddress), size);
        }

        public override void MaybePrefetchMemory(List<long> pagesToPrefetch)
        {
            if (PlatformDetails.CanPrefetch == false)
                return; // not supported

            if (pagesToPrefetch.Count == 0)
                return;

            var entries = new Win32MemoryMapNativeMethods.WIN32_MEMORY_RANGE_ENTRY[pagesToPrefetch.Count];
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i].NumberOfBytes = (IntPtr)(4 * PageSize);
                entries[i].VirtualAddress = AcquirePagePointer(null, pagesToPrefetch[i]);
            }

            fixed (Win32MemoryMapNativeMethods.WIN32_MEMORY_RANGE_ENTRY* entriesPtr = entries)
            {
                Win32MemoryMapNativeMethods.PrefetchVirtualMemory(Win32Helper.CurrentProcess,
                    (UIntPtr)PagerState.AllocationInfos.Length, entriesPtr, 0);
            }
        }

        public override void TryPrefetchingWholeFile()
        {
            if (PlatformDetails.CanPrefetch == false)
                return; // not supported

            var pagerState = PagerState;
            var entries =
                stackalloc Win32MemoryMapNativeMethods.WIN32_MEMORY_RANGE_ENTRY[pagerState.AllocationInfos.Length];

            for (var i = 0; i < pagerState.AllocationInfos.Length; i++)
            {
                entries[i].VirtualAddress = pagerState.AllocationInfos[i].BaseAddress;
                entries[i].NumberOfBytes = (IntPtr)pagerState.AllocationInfos[i].Size;
            }

            if (Win32MemoryMapNativeMethods.PrefetchVirtualMemory(Win32Helper.CurrentProcess,
                (UIntPtr)pagerState.AllocationInfos.Length, entries, 0) == false)
                throw new Win32Exception();
        }


        internal override void ProtectPageRange(byte* start, ulong size, bool force = false)
        {
            if (size == 0)
                return;

            if (UsePageProtection || force)
            {
                Win32NativeMethods.MEMORY_BASIC_INFORMATION memoryInfo1 = new Win32NativeMethods.MEMORY_BASIC_INFORMATION();
                int vQueryFirstOutput = Win32NativeMethods.VirtualQuery(start, &memoryInfo1, new UIntPtr(size));
                int vQueryFirstError = Marshal.GetLastWin32Error();

                Win32NativeMethods.MemoryProtection oldProtection;
                bool status = Win32NativeMethods.VirtualProtect(start, new UIntPtr(size), Win32NativeMethods.MemoryProtection.READONLY, out oldProtection);
                if (!status)
                {
                    int vProtectError = Marshal.GetLastWin32Error();

                    Win32NativeMethods.MEMORY_BASIC_INFORMATION memoryInfo2 = new Win32NativeMethods.MEMORY_BASIC_INFORMATION();
                    int vQuerySecondOutput = Win32NativeMethods.VirtualQuery(start, &memoryInfo2, new UIntPtr(size));
                    int vQuerySecondError = Marshal.GetLastWin32Error();
                    Debugger.Break();
                }
            }
        }

        internal override void UnprotectPageRange(byte* start, ulong size, bool force = false)
        {
            if (size == 0)
                return;

            if (UsePageProtection || force)
            {
                Win32NativeMethods.MEMORY_BASIC_INFORMATION memoryInfo1 = new Win32NativeMethods.MEMORY_BASIC_INFORMATION();
                int vQueryFirstOutput = Win32NativeMethods.VirtualQuery(start, &memoryInfo1, new UIntPtr(size));
                int vQueryFirstError = Marshal.GetLastWin32Error();

                Win32NativeMethods.MemoryProtection oldProtection;
                bool status = Win32NativeMethods.VirtualProtect(start, new UIntPtr(size), Win32NativeMethods.MemoryProtection.READWRITE, out oldProtection);
                if (!status)
                {
                    int vProtectError = Marshal.GetLastWin32Error();

                    Win32NativeMethods.MEMORY_BASIC_INFORMATION memoryInfo2 = new Win32NativeMethods.MEMORY_BASIC_INFORMATION();
                    int vQuerySecondOutput = Win32NativeMethods.VirtualQuery(start, &memoryInfo2, new UIntPtr(size));
                    int vQuerySecondError = Marshal.GetLastWin32Error();
                    Debugger.Break();
                }
            }
        }
    }
}
