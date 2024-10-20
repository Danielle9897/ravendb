//-----------------------------------------------------------------------
// <copyright file="QueryResult.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

using Raven.Json.Linq;

namespace Raven.Client.Data.Queries
{
    /// <summary>
    /// The result of a query
    /// </summary>
    public class QueryResult<T> : QueryResultBase<T>
    {
        /// <summary>
        /// Gets or sets the total results for this query
        /// </summary>
        public int TotalResults { get; set; }

        /// <summary>
        /// Gets or sets the skipped results
        /// </summary>
        public int SkippedResults { get; set; }

        /// <summary>
        /// Highlighter results (if requested).
        /// </summary>
        public Dictionary<string, Dictionary<string, string[]>> Highlightings { get; set; }

        /// <summary>
        /// The duration of actually executing the query server side
        /// </summary>
        public long DurationMilliseconds { get; set; }

        /// <summary>
        /// Explanations of document scores (if requested).
        /// </summary>
        public Dictionary<string, string> ScoreExplanations { get; set; }

        /// <summary>
        /// Detailed timings for various parts of a query (Lucene search, loading documents, transforming results) - if requested.
        /// </summary>
        public Dictionary<string, double> TimingsInMilliseconds { get; set; }

        /// <summary>
        /// The size of the request which were sent from the server.
        /// This value is the _uncompressed_ size. 
        /// </summary>
        public long ResultSize { get; set; }
    }

    public class QueryResult : QueryResult<RavenJObject>
    {
        /// <summary>
        /// Ensures that the query results can be used in snapshots
        /// </summary>
        public void EnsureSnapshot()
        {
            foreach (var result in Results.Where(x => x != null))
            {
                result.EnsureCannotBeChangeAndEnableSnapshotting();
            }
            foreach (var result in Includes)
            {
                result.EnsureCannotBeChangeAndEnableSnapshotting();
            }
        }

        /// <summary>
        /// Creates a snapshot of the query results
        /// </summary>
        public QueryResult CreateSnapshot()
        {
            return new QueryResult
            {
                Results = new List<RavenJObject>(Results.Select(x => x != null ? (RavenJObject)x.CreateSnapshot() : null)),
                Includes = new List<RavenJObject>(Includes.Select(x => (RavenJObject)x.CreateSnapshot())),
                IndexName = IndexName,
                IndexTimestamp = IndexTimestamp,
                IsStale = IsStale,
                SkippedResults = SkippedResults,
                TotalResults = TotalResults,
                Highlightings = Highlightings == null ? null : Highlightings.ToDictionary(
                    pair => pair.Key,
                    x => new Dictionary<string, string[]>(x.Value)),
                ScoreExplanations = ScoreExplanations == null ? null : ScoreExplanations.ToDictionary(x => x.Key, x => x.Value),
                TimingsInMilliseconds = TimingsInMilliseconds == null ? null : TimingsInMilliseconds.ToDictionary(x => x.Key, x => x.Value),
                LastQueryTime = LastQueryTime,
                DurationMilliseconds = DurationMilliseconds,
                ResultEtag = ResultEtag
            };
        }
    }
}
