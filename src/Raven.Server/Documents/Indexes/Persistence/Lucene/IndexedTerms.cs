﻿// -----------------------------------------------------------------------
//  <copyright file="CachedIndexedTerms.cs" company="Hibernating Rhinos LTD"> 
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lucene.Net.Index;
using Lucene.Net.Util;
using Raven.Server.ServerWide.Context;
using Raven.Server.ServerWide.LowMemoryNotification;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Documents.Indexes.Persistence.Lucene
{
    internal static class IndexedTerms
    {
        private static readonly WeakCache CacheInstance = new WeakCache();

        public class WeakCache : ILowMemoryHandler
        {
            public ConditionalWeakTable<IndexReader, CachedIndexedTerms> TermsCachePerReader = new ConditionalWeakTable<IndexReader, CachedIndexedTerms>();

            public List<WeakReference<IndexReader>> Keys = new List<WeakReference<IndexReader>>();

            public WeakCache()
            {
                AbstractLowMemoryNotification.Instance.RegisterLowMemoryHandler(this);
            }

            public void HandleLowMemory()
            {
                lock (this)
                {
                    foreach (var reference in Keys)
                    {
                        IndexReader target;
                        if (reference.TryGetTarget(out target))
                            TermsCachePerReader.Remove(target);
                    }

                    Keys.Clear();
                }
            }

            public void SoftMemoryRelease()
            {
            }

            public LowMemoryHandlerStatistics GetStats()
            {
                return new LowMemoryHandlerStatistics
                {
                    Name = "WeakCache"
                };
            }
        }

        public static Dictionary<string, int[]> GetTermsAndDocumentsFor(IndexReader reader, int docBase, string field, string indexName)
        {
            var termsCachePerField = CacheInstance.TermsCachePerReader.GetValue(reader, x => new CachedIndexedTerms(indexName));

            FieldCacheInfo info;
            if (termsCachePerField.Results.TryGetValue(field, out info) && info.Done)
                return info.Results;

            info = termsCachePerField.Results.GetOrAdd(field, new FieldCacheInfo());
            if (info.Done)
                return info.Results;

            lock (info)
            {
                if (info.Done)
                    return info.Results;

                info.Results = FillCache(reader, docBase, field);
                info.Done = true;

                return info.Results;
            }
        }

        public static BlittableJsonReaderObject[] ReadAllEntriesFromIndex(IndexReader reader, JsonOperationContext context)
        {
            if (reader.MaxDoc > 512 * 1024)
            {
                throw new InvalidOperationException("Refusing to extract all index entires from an index with " + reader.MaxDoc +
                                                    " entries, because of the probable time / memory costs associated with that." +
                                                    Environment.NewLine +
                                                    "Viewing Index Entries are a debug tool, and should not be used on indexes of this size. You might want to try Luke, instead.");
            }

            var results = new DynamicJsonValue[reader.MaxDoc];
            using (var termDocs = reader.TermDocs())
            using (var termEnum = reader.Terms())
            {
                while (termEnum.Next())
                {
                    var term = termEnum.Term;
                    if (term == null)
                        break;

                    var text = term.Text;

                    termDocs.Seek(termEnum);
                    for (var i = 0; i < termEnum.DocFreq() && termDocs.Next(); i++)
                    {
                        var result = results[termDocs.Doc];
                        if (result == null)
                            results[termDocs.Doc] = result = new DynamicJsonValue();

                        var propertyName = term.Field;
                        if (propertyName.EndsWith("_ConvertToJson") ||
                            propertyName.EndsWith("_IsArray") ||
                            propertyName.EndsWith(Raven.Abstractions.Data.Constants.Indexing.Fields.RangeFieldSuffix))
                            continue;

                        var oldValue = result[propertyName];
                        if (oldValue != null)
                        {
                            var oldValueAsArray = oldValue as DynamicJsonArray;
                            if (oldValueAsArray != null)
                            {
                                oldValueAsArray.Add(text);
                                continue;
                            }

                            var oldValueAsString = oldValue as string;
                            if (oldValueAsString != null)
                            {
                                result[propertyName] = oldValueAsArray = new DynamicJsonArray();
                                oldValueAsArray.Add(oldValueAsString);
                                oldValueAsArray.Add(text);
                                continue;
                            }

                            throw new ArgumentException("No idea how to handle " + oldValue.GetType());
                        }

                        result[propertyName] = text;
                    }
                }
            }

            return results
                .Select(x => context.ReadObject(x, "index/entries"))
                .ToArray();
        }

        private static Dictionary<string, int[]> FillCache(IndexReader reader, int docBase, string field)
        {
            using (var termDocs = reader.TermDocs())
            {
                var items = new Dictionary<string, int[]>();
                var docsForTerm = new List<int>();
                using (var termEnum = reader.Terms(new Term(field)))
                {
                    do
                    {
                        if (termEnum.Term == null || field != termEnum.Term.Field)
                            break;

                        Term term = termEnum.Term;
                        if (LowPrecisionNumber(term.Field, term.Text))
                            continue;

                        var totalDocCountIncludedDeletes = termEnum.DocFreq();
                        termDocs.Seek(termEnum.Term);
                        while (termDocs.Next() && totalDocCountIncludedDeletes > 0)
                        {
                            var curDoc = termDocs.Doc;
                            totalDocCountIncludedDeletes -= 1;
                            if (reader.IsDeleted(curDoc))
                                continue;

                            docsForTerm.Add(curDoc + docBase);
                        }
                        docsForTerm.Sort();
                        items[term.Text] = docsForTerm.ToArray();
                        docsForTerm.Clear();
                    } while (termEnum.Next());
                }
                return items;
            }
        }

        private static bool LowPrecisionNumber(string field, string val)
        {
            if (field.EndsWith(Raven.Abstractions.Data.Constants.Indexing.Fields.RangeFieldSuffix) == false)
                return false;

            if (string.IsNullOrEmpty(val))
                return false;

            return val[0] - NumericUtils.SHIFT_START_INT != 0 &&
                   val[0] - NumericUtils.SHIFT_START_LONG != 0;
        }

        public class CachedIndexedTerms : ILowMemoryHandler
        {
            public readonly ConcurrentDictionary<string, FieldCacheInfo> Results = new ConcurrentDictionary<string, FieldCacheInfo>();
            private readonly object _indexName;

            public CachedIndexedTerms(string indexName)
            {
                _indexName = indexName;
                AbstractLowMemoryNotification.Instance.RegisterLowMemoryHandler(this);
            }

            public void HandleLowMemory()
            {
                Results.Clear();
            }

            public void SoftMemoryRelease()
            {
            }

            public LowMemoryHandlerStatistics GetStats()
            {
                return new LowMemoryHandlerStatistics
                {
                    Name = "CachedIndexedTerms",
                    Metadata = new
                    {
                        IndexName = _indexName
                    },
                    EstimatedUsedMemory = Results.Sum(x => x.Key.Length * sizeof(char) + x.Value.Results.Sum(y => y.Key.Length * sizeof(char) + y.Value.Length * sizeof(int)))
                };
            }
        }

        public class FieldCacheInfo
        {
            public Dictionary<string, int[]> Results;
            public bool Done;
        }
    }
}
