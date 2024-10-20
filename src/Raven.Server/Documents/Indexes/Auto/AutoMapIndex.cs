﻿using System;
using System.Collections.Generic;
using Raven.Client.Data.Indexes;
using Raven.Server.Config.Categories;
using Raven.Server.ServerWide.Context;
using Voron;

namespace Raven.Server.Documents.Indexes.Auto
{
    public class AutoMapIndex : MapIndexBase<AutoMapIndexDefinition>
    {
        private AutoMapIndex(int indexId, AutoMapIndexDefinition definition)
            : base(indexId, IndexType.AutoMap, definition)
        {
            ActualMaxNumberOfIndexOutputs = null;
            MaxNumberOfIndexOutputs = 1;
        }

        public static AutoMapIndex CreateNew(int indexId, AutoMapIndexDefinition definition, DocumentDatabase documentDatabase)
        {
            var instance = new AutoMapIndex(indexId, definition);
            instance.Initialize(documentDatabase, documentDatabase.Configuration.Indexing);

            return instance;
        }

        public static AutoMapIndex Open(int indexId, StorageEnvironment environment, DocumentDatabase documentDatabase)
        {
            var definition = AutoMapIndexDefinition.Load(environment);
            var instance = new AutoMapIndex(indexId, definition);
            instance.Initialize(environment, documentDatabase, documentDatabase.Configuration.Indexing);

            return instance;
        }

        public override IIndexedDocumentsEnumerator GetMapEnumerator(IEnumerable<Document> documents, string collection, TransactionOperationContext indexContext, IndexingStatsScope stats)
        {
            return new AutoIndexDocsEnumerator(documents, stats);
        }

        public override void Update(IndexDefinitionBase definition, IndexingConfiguration configuration)
        {
            throw new NotSupportedException($"{Type} index does not support updating it's definition and configuration.");
        }

        public override int? ActualMaxNumberOfIndexOutputs { get; }
        public override int MaxNumberOfIndexOutputs { get; }
    }
}