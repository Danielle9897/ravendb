﻿using System;
using System.Diagnostics;
using System.Threading;
using Raven.Abstractions.Data;
using Raven.Client.Data.Indexes;
using Raven.Server.Config.Categories;
using Raven.Server.Documents.Indexes.MapReduce;
using Raven.Server.Documents.Indexes.Persistence.Lucene;
using Raven.Server.ServerWide.Context;
using Sparrow.Logging;

namespace Raven.Server.Documents.Indexes.Workers
{
    public class CleanupDeletedDocuments : IIndexingWork
    {
        private readonly Logger _logger;

        private readonly Index _index;
        private readonly IndexingConfiguration _configuration;
        private readonly DocumentsStorage _documentsStorage;
        private readonly IndexStorage _indexStorage;
        private readonly MapReduceIndexingContext _mapReduceContext;

        public CleanupDeletedDocuments(Index index, DocumentsStorage documentsStorage, IndexStorage indexStorage,
            IndexingConfiguration configuration, MapReduceIndexingContext mapReduceContext)
        {
            _index = index;
            _configuration = configuration;
            _mapReduceContext = mapReduceContext;
            _documentsStorage = documentsStorage;
            _indexStorage = indexStorage;
            _logger = LoggingSource.Instance
                .GetLogger<CleanupDeletedDocuments>(indexStorage.DocumentDatabase.Name);
        }

        public string Name => "Cleanup";

        public bool Execute(DocumentsOperationContext databaseContext, TransactionOperationContext indexContext,
            Lazy<IndexWriteOperation> writeOperation, IndexingStatsScope stats, CancellationToken token)
        {
            var pageSize = int.MaxValue;
            var maxTimeForDocumentTransactionToRemainOpen = Debugger.IsAttached == false
                ? _configuration.MaxTimeForDocumentTransactionToRemainOpen.AsTimeSpan
                : TimeSpan.FromMinutes(15);

            var moreWorkFound = false;

            foreach (var collection in _index.Collections)
            {
                using (var collectionStats = stats.For("Collection_" + collection))
                {
                    if (_logger.IsInfoEnabled)
                        _logger.Info($"Executing cleanup for '{_index.Name} ({_index.IndexId})'. Collection: {collection}.");

                    var lastMappedEtag = _indexStorage.ReadLastIndexedEtag(indexContext.Transaction, collection);
                    var lastTombstoneEtag = _indexStorage.ReadLastProcessedTombstoneEtag(indexContext.Transaction,
                        collection);

                    if (_logger.IsInfoEnabled)
                        _logger.Info($"Executing cleanup for '{_index} ({_index.Name})'. LastMappedEtag: {lastMappedEtag}. LastTombstoneEtag: {lastTombstoneEtag}.");

                    var lastEtag = lastTombstoneEtag;
                    var count = 0;

                    var sw = new Stopwatch();
                    IndexWriteOperation indexWriter = null;
                    var keepRunning = true;
                    var lastCollectionEtag = -1L;
                    while (keepRunning)
                    {
                        var batchCount = 0;

                        using (databaseContext.OpenReadTransaction())
                        {
                            sw.Restart();

                            if (lastCollectionEtag == -1)
                                lastCollectionEtag = _index.GetLastTombstoneEtagInCollection(databaseContext, collection);

                            var tombstones = collection == Constants.Indexing.AllDocumentsCollection
                                ? _documentsStorage.GetTombstonesFrom(databaseContext, lastEtag + 1, 0, pageSize)
                                : _documentsStorage.GetTombstonesFrom(databaseContext, collection, lastEtag + 1, 0, pageSize);

                            foreach (var tombstone in tombstones)
                            {
                                token.ThrowIfCancellationRequested();

                                if (indexWriter == null)
                                    indexWriter = writeOperation.Value;

                                if (_logger.IsInfoEnabled)
                                    _logger.Info($"Executing cleanup for '{_index} ({_index.Name})'. Processing tombstone {tombstone.Key} ({tombstone.Etag}).");

                                count++;
                                batchCount++;
                                lastEtag = tombstone.Etag;

                                if (tombstone.DeletedEtag > lastMappedEtag)
                                    continue; // no-op, we have not yet indexed this document

                                _index.HandleDelete(tombstone, collection, indexWriter, indexContext, collectionStats);

                                if (CanContinueBatch(collectionStats, lastEtag, lastCollectionEtag) == false)
                                {
                                    keepRunning = false;
                                    break;
                                }

                                if (MapDocuments.MaybeRenewTransaction(databaseContext, sw, _configuration, ref maxTimeForDocumentTransactionToRemainOpen))
                                    break;
                            }

                            if (batchCount == 0 || batchCount >= pageSize)
                                break;
                        }
                    }

                    if (count == 0)
                        continue;

                    if (_logger.IsInfoEnabled)
                        _logger.Info($"Executing cleanup for '{_index} ({_index.Name})'. Processed {count} tombstones in '{collection}' collection in {collectionStats.Duration.TotalMilliseconds:#,#;;0} ms.");

                    if (_index.Type.IsMap())
                    {
                        _indexStorage.WriteLastTombstoneEtag(indexContext.Transaction, collection, lastEtag);
                    }
                    else
                    {
                        _mapReduceContext.ProcessedTombstoneEtags[collection] = lastEtag;
                    }

                    moreWorkFound = true;
                }
            }

            return moreWorkFound;
        }

        public bool CanContinueBatch(IndexingStatsScope stats, long currentEtag, long maxEtag)
        {
            if (stats.Duration >= _configuration.MapTimeout.AsTimeSpan)
                return false;

            if (currentEtag >= maxEtag && stats.Duration >= _configuration.MapTimeoutAfterEtagReached.AsTimeSpan)
                return false;

            if (_index.CanContinueBatch(stats) == false)
                return false;

            return true;
        }
    }
}