using System;
using System.Collections.Generic;

using Lucene.Net.Search;

using Raven.Abstractions.Data;
using Raven.Client.Data.Indexes;
using Raven.Server.Documents.Queries;
using Raven.Server.Documents.Queries.Results;

namespace Raven.Server.Documents.Indexes.Persistence.Lucene
{
    public class IndexQueryingScope : IDisposable
    {
        private readonly IndexType _indexType;

        private readonly IndexQueryServerSide _query;

        private readonly FieldsToFetch _fieldsToFetch;

        private readonly IndexSearcher _searcher;

        private readonly IQueryResultRetriever _retriever;

        private readonly bool _isSortingQuery;

        private readonly HashSet<ulong> _alreadySeenProjections;

        private readonly HashSet<string> _alreadySeenDocumentKeysInPreviousPage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private int _alreadyScannedForDuplicates;

        public bool HasMultipleIndexOutputs { get; private set; }

        public int MaxNumberOfIndexOutputs { get; }

        public IndexQueryingScope(IndexType indexType, IndexQueryServerSide query, FieldsToFetch fieldsToFetch, IndexSearcher searcher, IQueryResultRetriever retriever, int maxIndexOutputsPerDocument, int? actualMaxIndexOutputsPerDocument)
        {
            _indexType = indexType;
            _query = query;
            _fieldsToFetch = fieldsToFetch;
            _searcher = searcher;
            _retriever = retriever;
            _isSortingQuery = query.SortedFields != null && query.SortedFields.Length > 0;

            if (_fieldsToFetch.IsDistinct)
                _alreadySeenProjections = new HashSet<ulong>();

            if (actualMaxIndexOutputsPerDocument.HasValue)
            {
                HasMultipleIndexOutputs = true;
                MaxNumberOfIndexOutputs = actualMaxIndexOutputsPerDocument.Value;
            }
            else
            {
                MaxNumberOfIndexOutputs = maxIndexOutputsPerDocument;

                if (MaxNumberOfIndexOutputs == -1) // configuration was set to disable output count check, probably because there exist fanout indexes
                    MaxNumberOfIndexOutputs = 50;
            }
        }

        public void Dispose()
        {
        }

        public void RecordAlreadyPagedItemsInPreviousPage(TopDocs search)
        {
            if (_query.Start == 0)
                return;

            if (_query.SkipDuplicateChecking)
                return;

            // we are paging, we need to check that we don't have duplicates in the previous pages
            // see here for details: http://groups.google.com/group/ravendb/browse_frm/thread/d71c44aa9e2a7c6e

            if (_indexType.IsMap() && _fieldsToFetch.IsProjection == false && search.ScoreDocs.Length >= _query.Start)
            {
                if (_isSortingQuery)
                {
                    // we need to scan all records from the beginning to requested 'start' position
                    for (var i = 0; i < _query.Start && i < search.ScoreDocs.Length; i++)
                    {
                        var scoreDoc = search.ScoreDocs[i];
                        var document = _searcher.Doc(scoreDoc.Doc);
                        var alreadyPagedKey = document.Get(Constants.Indexing.Fields.DocumentIdFieldName);

                        _alreadySeenDocumentKeysInPreviousPage.Add(alreadyPagedKey);
                        HasMultipleIndexOutputs = true;
                    }
                }
                else
                {
                    // that's not a sorted query so we need just to ensure that we won't return the last item of the previous page
                    var scoreDoc = search.ScoreDocs[_query.Start - 1];
                    var document = _searcher.Doc(scoreDoc.Doc);
                    var alreadyPagedKey = document.Get(Constants.Indexing.Fields.DocumentIdFieldName);

                    _alreadySeenDocumentKeysInPreviousPage.Add(alreadyPagedKey);
                    HasMultipleIndexOutputs = true;
                }
            }

            if (_fieldsToFetch.IsDistinct == false)
                return;

            for (; _alreadyScannedForDuplicates < _query.Start; _alreadyScannedForDuplicates++)
            {
                var scoreDoc = search.ScoreDocs[_alreadyScannedForDuplicates];
                var document = _retriever.Get(_searcher.Doc(scoreDoc.Doc), scoreDoc.Score);

                if (document.Data.Count > 0) // we don't consider empty projections to be relevant for distinct operations
                    _alreadySeenProjections.Add(document.DataHash);
            }
        }

        public bool WillProbablyIncludeInResults(string key)
        {
            if (_fieldsToFetch.IsDistinct)
                return true;

            if (_indexType.IsMapReduce())
                return true;

            if (_query.SkipDuplicateChecking)
                return true;

            if (_fieldsToFetch.IsProjection && _alreadySeenDocumentKeysInPreviousPage.Contains(key))
            {
                HasMultipleIndexOutputs = true;
                return false;
            }

            if (_fieldsToFetch.IsProjection == false && _alreadySeenDocumentKeysInPreviousPage.Add(key) == false)
            {
                HasMultipleIndexOutputs = true;
                if (_fieldsToFetch.IsTransformation && _query.AllowMultipleIndexEntriesForSameDocumentToResultTransformer)
                    return true;

                return false;
            }

            return true;
        }

        public bool TryIncludeInResults(Document document)
        {
            //if (shouldIncludeInResults(indexQueryResult) == false)
            //    return false;
            if (document == null)
                return false;

            if (_fieldsToFetch.IsDistinct)
                return _alreadySeenProjections.Add(document.DataHash);
            
            return true;
        }
    }
}