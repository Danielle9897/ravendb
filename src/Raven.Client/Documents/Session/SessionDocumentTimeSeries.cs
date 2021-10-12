﻿//-----------------------------------------------------------------------
// <copyright file="SessionDocumentTimeSeries.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client.Documents.Session.Loaders;
using Raven.Client.Documents.Session.TimeSeries;
using Raven.Client.Util;

namespace Raven.Client.Documents.Session
{
    public class SessionDocumentTimeSeries<TValues> : ISessionDocumentTimeSeries, ISessionDocumentRollupTypedTimeSeries<TValues>,
        ISessionDocumentTypedTimeSeries<TValues>, ISessionDocumentIncrementalTimeSeries, ISessionDocumentTypedIncrementalTimeSeries<TValues> where TValues : new()
    {
        private readonly AsyncSessionDocumentTimeSeries<TimeSeriesEntry> _asyncSessionTimeSeries;

        public SessionDocumentTimeSeries(InMemoryDocumentSessionOperations session, string documentId, string name)
        {
            _asyncSessionTimeSeries = new AsyncSessionDocumentTimeSeries<TimeSeriesEntry>(session, documentId, name);
        }

        public SessionDocumentTimeSeries(InMemoryDocumentSessionOperations session, object entity, string name)
        {
            _asyncSessionTimeSeries = new AsyncSessionDocumentTimeSeries<TimeSeriesEntry>(session, entity, name);
        }

        public void Append(DateTime timestamp, IEnumerable<double> values, string tag = null)
        {
            _asyncSessionTimeSeries.Append(timestamp, values, tag);
        }

        public void Append(DateTime timestamp, double value, string tag = null)
        {
            _asyncSessionTimeSeries.Append(timestamp, value, tag);
        }

        public void Append(DateTime timestamp, TValues value, string tag = null)
        {
            _asyncSessionTimeSeries.Append(timestamp, value, tag);
        }

        public void Append(TimeSeriesEntry<TValues> entry)
        {
            _asyncSessionTimeSeries.Append(entry.Timestamp, entry.Value, entry.Tag);
        }
        
        public TimeSeriesEntry[] Get(DateTime? from = null, DateTime? to = null, int start = 0, int pageSize = int.MaxValue)
        {
            return Get(from, to, includes: null, start, pageSize);
        }

        public TimeSeriesEntry[] Get(DateTime? from, DateTime? to, Action<ITimeSeriesIncludeBuilder> includes, int start = 0, int pageSize = int.MaxValue)
        {
            return AsyncHelpers.RunSync(() => _asyncSessionTimeSeries.GetAsync(from, to, includes, start, pageSize));
        }

        private TimeSeriesEntry<TValues>[] GetInternal(DateTime? from, DateTime? to, int start, int pageSize)
        {
            return AsyncHelpers.RunSync(() =>
            {
                if (_asyncSessionTimeSeries.NotInCache(from, to))
                {
                    return _asyncSessionTimeSeries.GetTimeSeriesAndIncludes<TimeSeriesEntry<TValues>>(from, to, includes: null, start, pageSize);
                }

                return _asyncSessionTimeSeries.GetTypedFromCache<TValues>(from, to, null, start, pageSize);
            });
        }

        TimeSeriesEntry<TValues>[] ISessionDocumentTypedTimeSeries<TValues>.Get(DateTime? from, DateTime? to, int start, int pageSize)
        {
            return GetInternal(from, to, start, pageSize);
        }

        TimeSeriesRollupEntry<TValues>[] ISessionDocumentRollupTypedTimeSeries<TValues>.Get(DateTime? from, DateTime? to, int start, int pageSize)
        {
            if (_asyncSessionTimeSeries.NotInCache(from, to))
            {
                return AsyncHelpers.RunSync(() => _asyncSessionTimeSeries.GetTimeSeriesAndIncludes<TimeSeriesRollupEntry<TValues>>(from, to, includes: null, start, pageSize));
            }

            var result = _asyncSessionTimeSeries.GetTypedFromCache<TValues>(from, to, includes: null, start, pageSize);
            return result.Result?.Select(r => r.AsRollupEntry()).ToArray();
        }

        public void Append(TimeSeriesRollupEntry<TValues> entry)
        {
            entry.SetValuesFromMembers();
            _asyncSessionTimeSeries.Append(entry.Timestamp, entry.Values, entry.Tag);
        }

        TimeSeriesEntry<TValues>[] ISessionDocumentTypedIncrementalTimeSeries<TValues>.Get(DateTime? from, DateTime? to, int start, int pageSize)
        {
            return GetInternal(from, to, start, pageSize);
        }

        public void Delete(DateTime? from = null, DateTime? to = null)
        {
            _asyncSessionTimeSeries.Delete(from, to);
        }

        public void Delete(DateTime at)
        {
            _asyncSessionTimeSeries.Delete(at);
        }

        IEnumerator<TimeSeriesEntry> ITimeSeriesStreamingBase<TimeSeriesEntry>.Stream(DateTime? @from, DateTime? to, TimeSpan? offset)
        {
            return AsyncHelpers.RunSync(() => _asyncSessionTimeSeries.GetStream<TimeSeriesEntry>(from, to, offset));
        }

        IEnumerator<TimeSeriesRollupEntry<TValues>> ITimeSeriesStreamingBase<TimeSeriesRollupEntry<TValues>>.Stream(DateTime? from, DateTime? to, TimeSpan? offset)
        {
            return AsyncHelpers.RunSync(() => _asyncSessionTimeSeries.GetStream<TimeSeriesRollupEntry<TValues>>(from, to, offset));
        }

        IEnumerator<TimeSeriesEntry<TValues>> ITimeSeriesStreamingBase<TimeSeriesEntry<TValues>>.Stream(DateTime? from, DateTime? to, TimeSpan? offset)
        {
            return AsyncHelpers.RunSync(() => _asyncSessionTimeSeries.GetStream<TimeSeriesEntry<TValues>>(from, to, offset));
        }

        public void Increment(DateTime timestamp, IEnumerable<double> values)
        {
            _asyncSessionTimeSeries.Increment(timestamp, values);
        }

        public void Increment(IEnumerable<double> values)
        {
            Increment(DateTime.UtcNow, values);
        }

        public void Increment(DateTime timestamp, double value)
        {
            Increment(timestamp, new[] { value });
        }
        public void Increment(double value)
        {
            Increment(DateTime.UtcNow, new[] { value });
        }
    }
}
