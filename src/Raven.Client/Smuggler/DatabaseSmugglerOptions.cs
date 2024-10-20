﻿using System;
using System.Collections.Generic;
using System.Text;
using Raven.Abstractions.Data;

namespace Raven.Client.Smuggler
{
    public class DatabaseSmugglerOptions
    {
        private const DatabaseItemType DefaultOperateOnTypes = DatabaseItemType.Indexes | DatabaseItemType.Transformers | DatabaseItemType.Documents | DatabaseItemType.RevisionDocuments | DatabaseItemType.Identities;

        private const int DefaultMaxStepsForTransformScript = 10 * 1000;

        public DatabaseSmugglerOptions()
        {
            OperateOnTypes = DefaultOperateOnTypes;
            MaxStepsForTransformScript = DefaultMaxStepsForTransformScript;
            CollectionsToExport = new List<string>();
            IncludeExpired = true;
        }

        public DatabaseItemType OperateOnTypes { get; set; }

        public int? RevisionDocumentsLimit { get; set; }

        public bool IncludeExpired { get; set; }

        public bool RemoveAnalyzers { get; set; }

        /// <summary>
        /// If true the import process will remove VersioningBundle from the document metadata
        /// </summary>
        public bool DisableVersioningBundle { get; set; }

        public string TransformScript { get; set; }

        public string FileName { get; set; }

        public List<string> CollectionsToExport { get; set; }

        /// <summary>
        /// Maximum number of steps that transform script can have
        /// </summary>
        public int MaxStepsForTransformScript { get; set; }

        public string Database { get; set; }

        public string ToQueryString()
        {
            var sb = new StringBuilder();

            if (OperateOnTypes != DefaultOperateOnTypes)
                sb.Append($"operateOnTypes={OperateOnTypes}");

            if (RevisionDocumentsLimit.HasValue)
                sb.Append($"&revisionDocumentsLimit={RevisionDocumentsLimit.Value}");

            if (IncludeExpired == false)
                sb.Append("&includeExpired=false");

            if (RemoveAnalyzers)
                sb.Append("&removeAnalyzers=true");

            if (DisableVersioningBundle)
                sb.Append("&disableVersioningBundle=true");

            if (string.IsNullOrWhiteSpace(TransformScript) == false)
                sb.Append($"&transformScript={Uri.EscapeDataString(TransformScript)}");

            CollectionsToExport.ApplyIfNotNull(collection => sb.AppendFormat("&collection={0}", Uri.EscapeDataString(collection)));

            if (MaxStepsForTransformScript != DefaultMaxStepsForTransformScript)
                sb.Append($"&maxStepsForTransformScript={MaxStepsForTransformScript}");

            return sb.ToString();
        }
    }
}
