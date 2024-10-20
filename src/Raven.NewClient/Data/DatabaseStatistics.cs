//-----------------------------------------------------------------------
// <copyright file="DatabaseStatistics.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using Raven.NewClient.Abstractions.Data;
using Raven.NewClient.Abstractions.Indexing;
using Raven.NewClient.Client.Indexing;
using Raven.NewClient.Data.Indexes;

namespace Raven.NewClient.Client.Data
{
    public class DatabaseStatistics
    {
        /// <summary>
        /// Last document etag in database.
        /// </summary>
        public long? LastDocEtag { get; set; }

        /// <summary>
        /// Total number of indexes in database.
        /// </summary>
        public int CountOfIndexes { get; set; }

        /// <summary>
        /// Total number of transformers in database.
        /// </summary>
        public int CountOfTransformers { get; set; }

        /// <summary>
        /// Total number of documents in database.
        /// </summary>
        public long CountOfDocuments { get; set; }

        /// <summary>
        /// Total number of revision documents in database.
        /// </summary>
        public long? CountOfRevisionDocuments { get; set; }

        /// <summary>
        /// List of stale index names in database..
        /// </summary>
        public string[] StaleIndexes => Indexes?.Where(x => x.IsStale).Select(x => x.Name).ToArray();

        /// <summary>
        /// Statistics for each index in database.
        /// </summary>
        public IndexInformation[] Indexes { get; set; }

        /// <summary>
        /// Database identifier.
        /// </summary>
        public Guid DatabaseId { get; set; }

        /// <summary>
        /// Indicates if process is 64-bit
        /// </summary>
        public bool Is64Bit { get; set; }
    }

    public class IndexInformation
    {
        public int IndexId { get; set; }

        public string Name { get; set; }

        public bool IsStale { get; set; }

        public IndexState State { get; set; }

        public IndexLockMode LockMode { get; set; }

        public IndexType Type { get; set; }
    }

    public class TriggerInfo
    {
        public string Type { get; set; }

        public string Name { get; set; }
    }

    public class PluginsInfo
    {
        public List<ExtensionsLog> Extensions { get; set; }
        public List<TriggerInfo> Triggers { get; set; }
        public List<string> CustomBundles { get; set; }
    }
}
