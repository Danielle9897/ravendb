﻿using System;
using System.Collections.Generic;
using Raven.Server.ServerWide.Context;
using Sparrow;
using Sparrow.Binary;
using Sparrow.Json;
using Voron;
using Voron.Data.BTrees;
using Voron.Data.Compression;
using Voron.Impl;
using Voron.Util;

namespace Raven.Server.Documents.Indexes.MapReduce
{
    public unsafe class MapReduceResultsStore : IDisposable
    {
        public const string ReduceTreePrefix = "#reduceTree-";
        public const string NestedValuesPrefix = "#nestedSection-";

        private readonly ulong _reduceKeyHash;
        private readonly TransactionOperationContext _indexContext;
        private readonly MapReduceIndexingContext _mapReduceContext;
        private readonly Slice _nestedValueKey;
        private ByteStringContext.InternalScope _nestedValueKeyScope;
        private readonly Transaction _tx;
        private readonly PageLocator _pageLocator;

        private NestedMapResultsSection _nestedSection;

        public MapResultsStorageType Type { get; private set; }

        public Tree Tree;
        public HashSet<long> ModifiedPages;
        public HashSet<long> FreedPages;

        public MapReduceResultsStore(ulong reduceKeyHash, MapResultsStorageType type, TransactionOperationContext indexContext, MapReduceIndexingContext mapReduceContext, bool create, PageLocator pageLocator = null)
        {
            _reduceKeyHash = reduceKeyHash;
            Type = type;
            _indexContext = indexContext;
            _mapReduceContext = mapReduceContext;
            _tx = indexContext.Transaction.InnerTransaction;
            _pageLocator = pageLocator;

            switch (Type)
            {
                case MapResultsStorageType.Tree:
                    InitializeTree(create);
                    break;
                case MapResultsStorageType.Nested:
                    _nestedValueKeyScope = Slice.From(indexContext.Allocator, NestedValuesPrefix + reduceKeyHash, ByteStringType.Immutable, out _nestedValueKey);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(Type.ToString());
            }
        }

        private void InitializeTree(bool create)
        {
            //TODO: Need better way to handle tree names

            var treeName = ReduceTreePrefix + _reduceKeyHash;
            Tree = create ? _tx.CreateTree(treeName, flags: TreeFlags.LeafsCompressed, pageLocator: _pageLocator) : _tx.ReadTree(treeName, pageLocator: _pageLocator);

            ModifiedPages = new HashSet<long>();
            FreedPages = new HashSet<long>();

            Tree.PageModified += page =>
            {
                ModifiedPages.Add(page);
                FreedPages.Remove(page);
            };

            Tree.PageFreed += page =>
            {
                FreedPages.Add(page);
                ModifiedPages.Remove(page);
            };
        }

        public void Delete(long id)
        {
            switch (Type)
            {
                case MapResultsStorageType.Tree:
                    Slice entrySlice;
                    using (Slice.External(_indexContext.Allocator, (byte*) &id, sizeof(long), out entrySlice))
                        Tree.Delete(entrySlice);
                    break;
                case MapResultsStorageType.Nested:
                    var section = GetNestedResultsSection();

                    section.Delete(id);

                    break;
                default:
                    throw new ArgumentOutOfRangeException(Type.ToString());
            }
        }

        public void Add(long id, BlittableJsonReaderObject result)
        {
            switch (Type)
            {
                case MapResultsStorageType.Tree:
                    Slice entrySlice;
                    
                    using (Slice.External(_indexContext.Allocator, (byte*) &id, sizeof(long), out entrySlice))
                    {
                        var pos = Tree.DirectAdd(entrySlice, result.Size);
                        result.CopyTo(pos);
                    }

                    break;
                case MapResultsStorageType.Nested:
                    var section = GetNestedResultsSection();

                    if (_mapReduceContext.ReducePhaseTree.ShouldGoToOverflowPage(_nestedSection.SizeAfterAdding(result)))
                    {
                        // would result in an overflow, that would be a space waste anyway, let's move to tree mode
                        MoveExistingResultsToTree(section);
                        Add(id, result); // now re-add the value
                    }
                    else
                    {
                        section.Add(id, result);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(Type.ToString());
            }
        }

        public ReadMapEntryScope Get(long id)
        {
            switch (Type)
            {
                case MapResultsStorageType.Tree:
                    Slice entrySlice;
                    using (Slice.External(_indexContext.Allocator, (byte*)&id, sizeof(long), out entrySlice))
                    {
                        var read = Tree.ReadDecompressed(entrySlice);

                        if (read == null)
                            throw new InvalidOperationException($"Could not find a map result with id '{id}' in '{Tree.Name}' tree");

                        return new ReadMapEntryScope(read);
                    }
                case MapResultsStorageType.Nested:
                    var section = GetNestedResultsSection();
                    return new ReadMapEntryScope(section.Get(id));
                default:
                    throw new ArgumentOutOfRangeException(Type.ToString());
            }
        }

        private void MoveExistingResultsToTree(NestedMapResultsSection section)
        {
            Type = MapResultsStorageType.Tree;

            var byteType = (byte)Type;

            Slice val;
            using (Slice.External(_indexContext.Allocator, &byteType, sizeof(byte), out val))
                _mapReduceContext.ResultsStoreTypes.Add((long)_reduceKeyHash, val);
            InitializeTree(create: true);

            section.MoveTo(Tree);
        }

        public NestedMapResultsSection GetNestedResultsSection(Tree tree = null) // TODO arek
        {
            if (_nestedSection != null)
                return _nestedSection;

            _nestedSection = new NestedMapResultsSection(_indexContext.Environment, tree ?? _mapReduceContext.ReducePhaseTree, _nestedValueKey);

            return _nestedSection;
        }

        public void Dispose()
        {
            _nestedValueKeyScope.Dispose();
        }
    }
}