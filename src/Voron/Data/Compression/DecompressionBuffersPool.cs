﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Sparrow.Binary;
using Voron.Data.BTrees;
using Voron.Global;
using Voron.Impl;
using Voron.Impl.Paging;
using Voron.Util;

namespace Voron.Data.Compression
{
    public unsafe class DecompressionBuffersPool : IDisposable
    {
        private readonly object _expandPoolLock = new object();
        private readonly object _decompressionPagerLock = new object();

        private readonly StorageEnvironmentOptions _options;

        private ConcurrentQueue<DecompressionBuffer>[] _pool;
        private long _decompressionPagerCounter;
        private long _lastUsedPage;
        private AbstractPager _compressionPager;
        private bool _initialized;

        private long _currentlyUsedBytes;

        private ImmutableAppendOnlyList<AbstractPager> _oldPagers;
        private readonly long _maxNumberOfPagesInScratchBufferPool;

        internal int NumberOfScratchFiles => 1 + _oldPagers.Count;

        public DecompressionBuffersPool(StorageEnvironmentOptions options)
        {
            _options = options;
            _maxNumberOfPagesInScratchBufferPool = _options.MaxScratchBufferSize / _options.PageSize;
        }

        public AbstractPager CreateDecompressionPager(long initialSize)
        {
            return _options.CreateScratchPager($"decompression.{_decompressionPagerCounter++:D10}.buffers", initialSize);
        }

        public DecompressedLeafPage GetPage(LowLevelTransaction tx, int pageSize, DecompressionUsage usage, TreePage original)
        {
            TemporaryPage tempPage;
            GetTemporaryPage(tx, pageSize, out tempPage);

            var treePage = tempPage.GetTempPage();

            return new DecompressedLeafPage(treePage.Base, treePage.PageSize, usage, original, tempPage);
        }

        public IDisposable GetTemporaryPage(LowLevelTransaction tx, int pageSize, out TemporaryPage tmp)
        {
            if (pageSize < _options.PageSize)
                ThrowInvalidPageSize(pageSize);

            if (pageSize > Constants.Compression.MaxPageSize)
                ThrowPageSizeTooBig(pageSize);

            Debug.Assert(pageSize == Bits.NextPowerOf2(pageSize));

            EnsureInitialized();

            var index = GetTempPagesPoolIndex(pageSize);

            if (_pool.Length <= index)
            {
                lock (_expandPoolLock)
                {
                    if (_pool.Length <= index) // someone could get the lock and add it meanwhile
                    {
                        var oldSize = _pool.Length;

                        var newPool = new ConcurrentQueue<DecompressionBuffer>[index + 1];
                        Array.Copy(_pool, newPool, _pool.Length);
                        for (var i = oldSize; i < newPool.Length; i++)
                        {
                            newPool[i] = new ConcurrentQueue<DecompressionBuffer>();
                        }
                        _pool = newPool;
                    }
                }
            }

            DecompressionBuffer buffer;

            var queue = _pool[index];

            tmp = null;

            while (queue.TryDequeue(out buffer))
            {
                if (buffer.CanReuse == false)
                    continue;

                try
                {
                    buffer.EnsureValidPointer(tx);
                    tmp = buffer.TempPage;
                }
                catch (ObjectDisposedException)
                {
                    // we could dispose the pager during the cleanup
                }
            }

            if (tmp == null)
            {
                var allocationInPages = pageSize / _options.PageSize;

                lock (_decompressionPagerLock) // once we fill up the pool we won't be allocating additional pages frequently
                {
                    if (_lastUsedPage + allocationInPages > _maxNumberOfPagesInScratchBufferPool)
                    {
                        _oldPagers = _oldPagers.Append(_compressionPager);
                        _compressionPager = CreateDecompressionPager(_options.MaxScratchBufferSize);
                        _lastUsedPage = 0;
                    }

                    _compressionPager.EnsureContinuous(_lastUsedPage, allocationInPages);

                    buffer = new DecompressionBuffer(_compressionPager, _lastUsedPage, pageSize, this, index, tx);

                    _lastUsedPage += allocationInPages;
                }

                tmp = buffer.TempPage;
            }

            Interlocked.Add(ref _currentlyUsedBytes, pageSize);
            
            return tmp.ReturnTemporaryPageToPool;
        }

        private static void ThrowPageSizeTooBig(int pageSize)
        {
            throw new ArgumentException($"Max page size is {Constants.Compression.MaxPageSize} while you requested {pageSize} bytes");
        }

        private void ThrowInvalidPageSize(int pageSize)
        {
            throw new ArgumentException(
                $"Page cannot be smaller than {_options.PageSize} bytes while {pageSize} bytes were requested.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (_decompressionPagerLock)
            {
                if (_initialized)
                    return;

                _pool = new[] { new ConcurrentQueue<DecompressionBuffer>() };
                _compressionPager = CreateDecompressionPager(DecompressedPagesCache.Size * Constants.Compression.MaxPageSize);
                _oldPagers = ImmutableAppendOnlyList<AbstractPager>.Empty;
                _initialized = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetTempPagesPoolIndex(int pageSize)
        {
            if (pageSize == _options.PageSize)
                return 0;

            var index = 0;

            while (pageSize > _options.PageSize)
            {
                pageSize >>= 1;
                index++;
            }
            return index;
        }
        public void Dispose()
        {
            if (_initialized == false)
                return;

            _compressionPager?.Dispose();

            foreach (var pager in _oldPagers)
            {
                pager.Dispose();
            }
        }

        public void Cleanup()
        {
            if (_initialized == false)
                return;

            if (_oldPagers.Count == 0)
                return;
            
            var necessaryPages = Interlocked.Read(ref _currentlyUsedBytes) / _options.PageSize;

            var availablePages = _compressionPager.NumberOfAllocatedPages;

            var pagers = _oldPagers;
            
            for (var i = pagers.Count - 1; i >= 0; i--)
            {
                var old = pagers[i];

                if (availablePages >= necessaryPages)
                {
                    old.Dispose();
                    continue;
                }

                availablePages += old.NumberOfAllocatedPages;
            }
            
            _oldPagers = _oldPagers.RemoveWhile(x => x.Disposed);
        }

        private class DecompressionBuffer : IDisposable
        {
            private readonly byte* _ptr;
            private readonly AbstractPager _pager;
            private readonly long _position;
            private readonly int _size;
            private readonly DecompressionBuffersPool _pool;
            private readonly int _index;

            public DecompressionBuffer(AbstractPager pager, long position, int size, DecompressionBuffersPool pool, int index, LowLevelTransaction tx)
            {
                _pager = pager;
                _position = position;
                _size = size;
                _pool = pool;
                _index = index;
                _pager.EnsureMapped(tx, _position, _size / _pager.PageSize);
                _ptr = _pager.AcquirePagePointer(tx, position);

                TempPage = new TemporaryPage(_ptr, size) { ReturnTemporaryPageToPool = this };
            }
            
            public readonly TemporaryPage TempPage;

            public void EnsureValidPointer(LowLevelTransaction tx)
            {
                _pager.EnsureMapped(tx, _position, _size / _pager.PageSize);
                var p = _pager.AcquirePagePointer(tx, _position);

                if (_ptr == p)
                    return;

                TempPage.SetPointer(p);
            }

            public bool CanReuse => _pager.Disposed == false;

            public void Dispose()
            {
                // return it to the pool
                _pool._pool[_index].Enqueue(this);

                Interlocked.Add(ref _pool._currentlyUsedBytes, -_size);
            }
        }
    }
}