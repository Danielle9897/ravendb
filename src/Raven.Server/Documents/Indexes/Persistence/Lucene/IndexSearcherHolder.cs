﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Sparrow.Logging;
using Lucene.Net.Search;
using Sparrow;
using Sparrow.Json;
using Voron.Impl;

namespace Raven.Server.Documents.Indexes.Persistence.Lucene
{
    public class IndexSearcherHolder
    {
        private readonly Func<IndexSearcher> _recreateSearcher;
        private readonly DocumentDatabase _documentDatabase;

        private readonly Logger _logger;
        private ImmutableList<IndexSearcherHoldingState> _states = ImmutableList<IndexSearcherHoldingState>.Empty;

        public IndexSearcherHolder(Func<IndexSearcher> recreateSearcher, DocumentDatabase documentDatabase)
        {
            _recreateSearcher = recreateSearcher;
            _documentDatabase = documentDatabase;
            _logger = LoggingSource.Instance.GetLogger<IndexSearcherHolder>(documentDatabase.Name);
        }

        public void SetIndexSearcher(Transaction asOfTx)
        {
            var state = new IndexSearcherHoldingState(asOfTx, _recreateSearcher, _documentDatabase.Name);

            _states = _states.Insert(0, state);

            Cleanup(asOfTx.LowLevelTransaction.Environment.PossibleOldestReadTransaction);
        }
        
        public IDisposable GetSearcher(Transaction tx, out IndexSearcher searcher)
        {
            var indexSearcherHoldingState = GetStateHolder(tx);
            try
            {
                searcher = indexSearcherHoldingState.IndexSearcher.Value;
                return indexSearcherHoldingState;
            }
            catch (Exception e)
            {
                if (_logger.IsInfoEnabled)
                    _logger.Info("Failed to get the index searcher.", e);
                indexSearcherHoldingState.Dispose();
                throw;
            }
        }

        internal IndexSearcherHoldingState GetStateHolder(Transaction tx)
        {
            var txId = tx.LowLevelTransaction.Id;

            foreach (var state in _states)
            {
                if (state.AsOfTxId > txId)
                {
                    continue;
                }

                Interlocked.Increment(ref state.Usage);

                return state;
            }

            throw new InvalidOperationException($"Could not get an index searcher state holder for transaction {txId}");
        }

        public void Cleanup(long oldestTx)
        {
            // note: cleanup cannot be called concurrently

            if (_states.Count == 1)
                return;
            
            // let's mark states which are no longer needed as ready for disposal

            for (var i = _states.Count - 1; i >= 1; i--)
            {
                var state = _states[i];

                if (state.AsOfTxId >= oldestTx)
                    break;

                var nextState = _states[i - 1];

                if (nextState.AsOfTxId > oldestTx)
                    break;

                Interlocked.Increment(ref state.Usage);

                using (state)
                {
                    state.MarkForDisposal();
                }

                _states = _states.Remove(state);
            }
        }

        internal class IndexSearcherHoldingState : IDisposable
        {
            private readonly Logger _logger;
            public readonly Lazy<IndexSearcher> IndexSearcher;

            public volatile bool ShouldDispose;
            public int Usage;
            public readonly long AsOfTxId;
            private readonly ConcurrentDictionary<Tuple<int, uint>, StringCollectionValue> _docsCache = new ConcurrentDictionary<Tuple<int, uint>, StringCollectionValue>();

            public IndexSearcherHoldingState(Transaction tx, Func<IndexSearcher> recreateSearcher, string dbName)
            {
                _logger = LoggingSource.Instance.GetLogger<IndexSearcherHolder>(dbName);
                IndexSearcher = new Lazy<IndexSearcher>(recreateSearcher, LazyThreadSafetyMode.ExecutionAndPublication);
                AsOfTxId = tx.LowLevelTransaction.Id;
            }

            ~IndexSearcherHoldingState()
            {
                if (_logger.IsInfoEnabled)
                    _logger.Info($"IndexSearcherHoldingState wasn't properly disposed. Usage count: {Usage}, tx id: {AsOfTxId}, should dispose: {ShouldDispose}");

                Dispose();
            }

            public void MarkForDisposal()
            {
                ShouldDispose = true;
            }

            public void Dispose()
            {
                if (Interlocked.Decrement(ref Usage) > 0)
                    return;

                if (ShouldDispose == false)
                    return;

                if (IndexSearcher.IsValueCreated)
                {
                    using (IndexSearcher.Value)
                    using (IndexSearcher.Value.IndexReader) { }
                }

                GC.SuppressFinalize(this);
            }

            public StringCollectionValue GetFieldsValues(int docId, uint fieldsHash, string[] fields, JsonOperationContext context)
            {
                var key = Tuple.Create(docId, fieldsHash);

                StringCollectionValue value;
                if (_docsCache.TryGetValue(key, out value))
                    return value;

                return _docsCache.GetOrAdd(key, _ =>
                {
                    var doc = IndexSearcher.Value.Doc(docId);
                    return new StringCollectionValue((from field in fields
                                                      from fld in doc.GetFields(field)
                                                      where fld.StringValue != null
                                                      select fld.StringValue).ToList(), context);
                });
            }
        }

        public class StringCollectionValue
        {
            private readonly int _hashCode;
            private readonly uint _hash;
#if DEBUG
            // ReSharper disable once NotAccessedField.Local
            private List<string> _values;
#endif

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                var other = obj as StringCollectionValue;
                if (other == null) return false;

                return _hash == other._hash;
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }

            public unsafe StringCollectionValue(List<string> values, JsonOperationContext context)
            {
#if DEBUG
                _values = values;
#endif
                if (values.Count == 0)
                    ThrowEmptyFacets();

                _hashCode = values.Count;
                _hash = (uint)values.Count;

                int size = 0;
                foreach (var value in values)
                {
                    size += value.Length;
                }
                var buffer = context.GetNativeTempBuffer(size * sizeof(char));
                var destChars = (char*)buffer;

                var position = 0;
                foreach (var value in values)
                {
                    for (var i = 0; i < value.Length; i++)
                        destChars[position++] = value[i];

                    unchecked
                    {
                        _hashCode = _hashCode * 397 ^ value.GetHashCode();
                    }
                }

                _hash = Hashing.XXHash32.Calculate(buffer, size);
            }

            private static void ThrowEmptyFacets()
            {
                throw new InvalidOperationException(
                    "Cannot apply distinct facet on empty fields, did you forget to store them in the index? ");
            }
        }
    }
}