﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Sparrow;
using Sparrow.Logging;
using Sparrow.Utils;
using Voron.Global;
using Voron.Impl.FileHeaders;
using Voron.Impl.Journal;
using Voron.Impl.Paging;
using Voron.Platform.Posix;
using Voron.Platform.Win32;
using Voron.Util;

namespace Voron
{
    public abstract class StorageEnvironmentOptions : IDisposable
    {
        public string TempPath { get; }

        public event EventHandler<RecoveryErrorEventArgs> OnRecoveryError;
        public event EventHandler<NonDurabalitySupportEventArgs> OnNonDurabaleFileSystemError;

        public abstract override string ToString();

        public void InvokeRecoveryError(object sender, string message, Exception e)
        {
            var handler = OnRecoveryError;
            if (handler == null)
            {
                throw new InvalidDataException(message + Environment.NewLine +
                     "An exception has been thrown because there isn't a listener to the OnRecoveryError event on the storage options.", e);
            }

            handler(this, new RecoveryErrorEventArgs(message, e));
        }

        public void InvokeNonDurabaleFileSystemError(object sender, string message, Exception e)
        {
            var handler = OnNonDurabaleFileSystemError;
            if (handler == null)
            {
                throw new InvalidDataException(message + Environment.NewLine +
                    "An exception has been thrown because there isn't a listener to the OnNonDurabaleFileSystemError event on the storage options.", e);
            }

            handler(this, new NonDurabalitySupportEventArgs(message, e));
        }

        public long? InitialFileSize { get; set; }

        public long MaxLogFileSize
        {
            get { return _maxLogFileSize; }
            set
            {
                if (value < _initialLogFileSize)
                    InitialLogFileSize = value;
                _maxLogFileSize = value;
            }
        }

        public long InitialLogFileSize
        {
            get { return _initialLogFileSize; }
            set
            {
                if (value > MaxLogFileSize)
                    MaxLogFileSize = value;
                _initialLogFileSize = value;
            }
        }

        public int PageSize
        {
            get { return _pageSize; }
            set
            {
                if (value > ushort.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(value), "PageSize must be less than " + ushort.MaxValue);
                if (value < 4 * Constants.Size.Kilobyte)
                    throw new ArgumentOutOfRangeException(nameof(value), $"PageSize must be higher than {4 * Constants.Size.Kilobyte} bytes");
                if (value % Constants.Size.Sector != 0)
                    throw new ArgumentException($"PageSize must be evenly divisible by {Constants.Size.Sector} (sector size)", nameof(value));

                _pageSize = value;
            }
        }

        // if set to a non zero value, will check that the expected schema is there
        public int SchemaVersion { get; set; }

        public long MaxScratchBufferSize { get; set; }

        public bool OwnsPagers { get; set; }

        public bool ManualFlushing { get; set; }

        public bool IncrementalBackupEnabled { get; set; }

        public abstract AbstractPager DataPager { get; }

        public long MaxNumberOfPagesInJournalBeforeFlush { get; set; }

        public int IdleFlushTimeout { get; set; }

        public long? MaxStorageSize { get; set; }

        public abstract string BasePath { get; }

        /// <summary>
        /// This mode is used in the Voron recovery tool and is not intended to be set otherwise.
        /// </summary>
        internal bool CopyOnWriteMode { get; set; }

        public abstract IJournalWriter CreateJournalWriter(long journalNumber, long journalSize);

        protected bool Disposed;
        private long _initialLogFileSize;
        private long _maxLogFileSize;
        private int _pageSize;
        public IoMetrics IoMetrics { get; set; }


        public Func<string, bool> ShouldUseKeyPrefix { get; set; }

        protected StorageEnvironmentOptions(string tempPath)
        {
            TempPath = tempPath;

            PageSize = Constants.Storage.PageSize;

            ShouldUseKeyPrefix = name => false;

            MaxLogFileSize = 256 * Constants.Size.Megabyte;

            InitialLogFileSize = 64 * Constants.Size.Kilobyte;

            MaxScratchBufferSize = 256 * Constants.Size.Megabyte;

            MaxNumberOfPagesInJournalBeforeFlush = 8192; // 32 MB when 4Kb             

            IdleFlushTimeout = 5000; // 5 seconds
            
            OwnsPagers = true;
            IncrementalBackupEnabled = false;

            IoMetrics = new IoMetrics(256, 256);

            _log = LoggingSource.Instance.GetLogger<StorageEnvironment>(tempPath);
        }

       
        public static StorageEnvironmentOptions CreateMemoryOnly(string name = null, string configTempPath = null)
        {
            if (configTempPath == null)
                configTempPath = Path.GetTempPath();

            return new PureMemoryStorageEnvironmentOptions(name, configTempPath);
        }

        public static StorageEnvironmentOptions ForPath(string path, string tempPath = null, string journalPath = null)
        {
            if (RunningOnPosix)
            {
                path = PosixHelper.FixLinuxPath(path);
                tempPath = PosixHelper.FixLinuxPath(tempPath);
            }
            return new DirectoryStorageEnvironmentOptions(path, tempPath, journalPath);
        }

        public class DirectoryStorageEnvironmentOptions : StorageEnvironmentOptions
        {
            private readonly string _journalPath;
            private readonly string _basePath;

            private readonly Lazy<AbstractPager> _dataPager;

            private readonly ConcurrentDictionary<string, Lazy<IJournalWriter>> _journals =
                new ConcurrentDictionary<string, Lazy<IJournalWriter>>(StringComparer.OrdinalIgnoreCase);

            public DirectoryStorageEnvironmentOptions(string basePath, string tempPath, string journalPath) : base(string.IsNullOrEmpty(tempPath) == false ? Path.GetFullPath(tempPath) : Path.GetFullPath(basePath))
            {
                _basePath = Path.GetFullPath(basePath);
                _journalPath = !string.IsNullOrEmpty(journalPath) ? Path.GetFullPath(journalPath) : _basePath;

                if (Directory.Exists(_basePath) == false)
                    Directory.CreateDirectory(_basePath);

                if (_basePath != tempPath && Directory.Exists(TempPath) == false)
                    Directory.CreateDirectory(TempPath);

                if (_journalPath != tempPath && Directory.Exists(_journalPath) == false)
                    Directory.CreateDirectory(_journalPath);

                _dataPager = new Lazy<AbstractPager>(() =>
                {
                    FilePath = Path.Combine(_basePath, Constants.DatabaseFilename);
                    if (RunningOnPosix)
                        return new PosixMemoryMapPager(this, FilePath, InitialFileSize, usePageProtection: true);

                    return new Win32MemoryMapPager(this, FilePath, InitialFileSize, usePageProtection: true);
                    //return new SparseMemoryMappedPager(this, FilePath, InitialFileSize);
                });
            }

            public string FilePath { get; private set; }

            public override string ToString()
            {
                return _basePath;
            }

            public override AbstractPager DataPager
            {
                get
                {
                    return _dataPager.Value;
                }
            }

            public override string BasePath
            {
                get { return _basePath; }
            }

            public override AbstractPager OpenPager(string filename)
            {
                if (RunningOnPosix)
                    return new PosixMemoryMapPager(this, filename);

                return new Win32MemoryMapPager(this, filename);
            }


            public override IJournalWriter CreateJournalWriter(long journalNumber, long journalSize)
            {
                var name = JournalName(journalNumber);
                var path = Path.Combine(_journalPath, name);
                var result = _journals.GetOrAdd(name, _ => new Lazy<IJournalWriter>(() =>
                {
                    if (RunningOnPosix)
                        return new PosixJournalWriter(this, path, journalSize);

                    return new Win32FileJournalWriter(this, path, journalSize);
                }));

                if (result.Value.Disposed)
                {
                    var newWriter = new Lazy<IJournalWriter>(() =>
                    {
                        if (RunningOnPosix)
                            return new PosixJournalWriter(this, path, journalSize);

                        return new Win32FileJournalWriter(this, path, journalSize);
                    });
                    if (_journals.TryUpdate(name, newWriter, result) == false)
                        throw new InvalidOperationException("Could not update journal pager");
                    result = newWriter;
                }

                return result.Value;
            }

            public override void Dispose()
            {
                if (Disposed)
                    return;
                Disposed = true;
                if (_dataPager.IsValueCreated)
                    _dataPager.Value.Dispose();
                foreach (var journal in _journals)
                {
                    if (journal.Value.IsValueCreated)
                        journal.Value.Value.Dispose();
                }
            }

            public override bool TryDeleteJournal(long number)
            {
                var name = JournalName(number);

                Lazy<IJournalWriter> lazy;
                if (_journals.TryRemove(name, out lazy) && lazy.IsValueCreated)
                    lazy.Value.Dispose();

                var file = Path.Combine(_journalPath, name);
                if (File.Exists(file) == false)
                    return false;
                File.Delete(file);
                return true;
            }

            public unsafe override bool ReadHeader(string filename, FileHeader* header)
            {
                var path = Path.Combine(_basePath, filename);
                if (File.Exists(path) == false)
                {
                    return false;
                }
                if (RunningOnPosix)
                    return PosixHelper.TryReadFileHeader(header, path);
                return Win32Helper.TryReadFileHeader(header, path);
            }


            public override unsafe void WriteHeader(string filename, FileHeader* header)
            {
                var path = Path.Combine(_basePath, filename);
                if (RunningOnPosix)
                    PosixHelper.WriteFileHeader(header, path);
                else
                    Win32Helper.WriteFileHeader(header, path);
            }

            public override void DeleteAllTempBuffers()
            {
                if (Directory.Exists(TempPath) == false)
                    return;

                foreach (var file in Directory.GetFiles(TempPath, "*.buffers"))
                    File.Delete(file);
            }

            public override AbstractPager CreateScratchPager(string name, long initialSize)
            {
                var scratchFile = Path.Combine(TempPath, name);
                if (File.Exists(scratchFile))
                    File.Delete(scratchFile);

                if (RunningOnPosix)
                {
                    return new PosixMemoryMapPager(this, scratchFile, initialSize)
                    {
                        DeleteOnClose = true
                    };
                }
                //return new SparseMemoryMappedPager(this, scratchFile,initialSize, (Win32NativeFileAttributes.DeleteOnClose | Win32NativeFileAttributes.Temporary));
                return new Win32MemoryMapPager(this, scratchFile, initialSize, (Win32NativeFileAttributes.DeleteOnClose | Win32NativeFileAttributes.Temporary));
            }

            public override AbstractPager OpenJournalPager(long journalNumber)
            {
                var name = JournalName(journalNumber);
                var path = Path.Combine(_journalPath, name);
                if (File.Exists(path) == false)
                    throw new InvalidOperationException("No such journal " + path);
                if (RunningOnPosix)
                    return new PosixMemoryMapPager(this, path);
                var win32MemoryMapPager = new Win32MemoryMapPager(this, path, access: Win32NativeFileAccess.GenericRead,
                    fileAttributes: Win32NativeFileAttributes.SequentialScan);
                win32MemoryMapPager.TryPrefetchingWholeFile();
                return win32MemoryMapPager;
                //return new SparseMemoryMappedPager(this, path,
                //    access: Win32NativeFileAccess.GenericRead,
                //    fileAttributes: Win32NativeFileAttributes.SequentialScan);
            }
        }

        public class PureMemoryStorageEnvironmentOptions : StorageEnvironmentOptions
        {
            private readonly string _name;
            private static int _counter;

            private readonly Lazy<AbstractPager> _dataPager;

            private readonly Dictionary<string, IJournalWriter> _logs =
                new Dictionary<string, IJournalWriter>(StringComparer.OrdinalIgnoreCase);

            private readonly Dictionary<string, IntPtr> _headers =
                new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
            private readonly int _instanceId;

            public PureMemoryStorageEnvironmentOptions(string name, string configTempPath) : base(configTempPath)
            {
                _name = name;
                _instanceId = Interlocked.Increment(ref _counter);
                var guid = Guid.NewGuid();
                var filename = $"ravendb-{Process.GetCurrentProcess().Id}-{_instanceId}-data.pager-{guid}";

                _dataPager = new Lazy<AbstractPager>(() =>
                {
                    if (RunningOnPosix)
                        return new PosixTempMemoryMapPager(this, Path.Combine(TempPath, filename), InitialFileSize);
                    return new Win32MemoryMapPager(this, Path.Combine(TempPath, filename), InitialFileSize, Win32NativeFileAttributes.RandomAccess | Win32NativeFileAttributes.DeleteOnClose | Win32NativeFileAttributes.Temporary);
                    //return new SparseMemoryMappedPager(this, Path.Combine(TempPath, filename), InitialFileSize);
                }, true);
            }

            public override string ToString()
            {
                return "mem #" + _instanceId + " " + _name;
            }

            public override AbstractPager DataPager => _dataPager.Value;

            public override string BasePath => ":memory:";

            public override IJournalWriter CreateJournalWriter(long journalNumber, long journalSize)
            {
                var name = JournalName(journalNumber);
                IJournalWriter value;
                if (_logs.TryGetValue(name, out value))
                    return value;
                value = new PureMemoryJournalWriter(this, journalSize);
                _logs[name] = value;
                return value;
            }

            public override void Dispose()
            {
                if (Disposed)
                    return;
                Disposed = true;

                _dataPager.Value.Dispose();
                foreach (var virtualPager in _logs)
                {
                    virtualPager.Value.Dispose();
                }

                foreach (var headerSpace in _headers)
                {
                    Marshal.FreeHGlobal(headerSpace.Value);
                }

                _headers.Clear();
            }

            public override bool TryDeleteJournal(long number)
            {
                var name = JournalName(number);
                IJournalWriter value;
                if (_logs.TryGetValue(name, out value) == false)
                    return false;
                _logs.Remove(name);
                value.Dispose();
                return true;
            }

            public override unsafe bool ReadHeader(string filename, FileHeader* header)
            {
                if (Disposed)
                    throw new ObjectDisposedException("PureMemoryStorageEnvironmentOptions");
                IntPtr ptr;
                if (_headers.TryGetValue(filename, out ptr) == false)
                {
                    return false;
                }
                *header = *((FileHeader*)ptr);
                return true;
            }

            public override unsafe void WriteHeader(string filename, FileHeader* header)
            {
                if (Disposed)
                    throw new ObjectDisposedException("PureMemoryStorageEnvironmentOptions");

                IntPtr ptr;
                if (_headers.TryGetValue(filename, out ptr) == false)
                {
                    ptr = (IntPtr)NativeMemory.AllocateMemory(sizeof(FileHeader));
                    _headers[filename] = ptr;
                }
                Memory.Copy((byte*)ptr, (byte*)header, sizeof(FileHeader));
            }

            public override void DeleteAllTempBuffers()
            {
                //no-op
            }

            public override AbstractPager CreateScratchPager(string name, long intialSize)
            {
                var guid = Guid.NewGuid();
                var filename = $"ravendb-{Process.GetCurrentProcess().Id}-{_instanceId}-{name}-{guid}";

                if (RunningOnPosix)
                    return new PosixTempMemoryMapPager(this, Path.Combine(TempPath, filename), intialSize);

                //return new SparseMemoryMappedPager(this, Path.Combine(TempPath, filename), intialSize,
                //     Win32NativeFileAttributes.RandomAccess | Win32NativeFileAttributes.DeleteOnClose | Win32NativeFileAttributes.Temporary);
                return new Win32MemoryMapPager(this, Path.Combine(TempPath, filename), intialSize,
                        Win32NativeFileAttributes.RandomAccess | Win32NativeFileAttributes.DeleteOnClose | Win32NativeFileAttributes.Temporary);
            }

            public override AbstractPager OpenPager(string filename)
            {
                if (RunningOnPosix)
                    return new PosixMemoryMapPager(this, filename);
                return new Win32MemoryMapPager(this, filename);
                //return new SparseMemoryMappedPager(this, filename);
            }

            public override AbstractPager OpenJournalPager(long journalNumber)
            {
                var name = JournalName(journalNumber);
                IJournalWriter value;
                if (_logs.TryGetValue(name, out value))
                    return value.CreatePager();
                throw new InvalidOperationException("No such journal " + journalNumber);
            }
        }

        public static string JournalName(long number)
        {
            return string.Format("{0:D19}.journal", number);
        }

        public static string JournalRecoveryName(long number)
        {
            return string.Format("{0:D19}.recovery", number);
        }

        public static string ScratchBufferName(long number)
        {
            return string.Format("scratch.{0:D10}.buffers", number);
        }

        public abstract void Dispose();

        public abstract bool TryDeleteJournal(long number);

        public abstract unsafe bool ReadHeader(string filename, FileHeader* header);

        public abstract unsafe void WriteHeader(string filename, FileHeader* header);

        public abstract void DeleteAllTempBuffers();

        public abstract AbstractPager CreateScratchPager(string name, long initialSize);

        public abstract AbstractPager OpenJournalPager(long journalNumber);

        public abstract AbstractPager OpenPager(string filename);

        public static bool RunningOnPosix
            => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
               RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public TransactionsMode TransactionsMode { get; set; }
        public OpenFlags PosixOpenFlags;
        public Win32NativeFileAttributes WinOpenFlags = SafeWin32OpenFlags;
        public DateTime? NonSafeTransactionExpiration { get; set; }


        public const Win32NativeFileAttributes SafeWin32OpenFlags = Win32NativeFileAttributes.Write_Through | Win32NativeFileAttributes.NoBuffering;
        public OpenFlags SafePosixOpenFlags = OpenFlags.O_DSYNC | PerPlatformValues.OpenFlags.O_DIRECT;
        private readonly Logger _log;
    }
}
