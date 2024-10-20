﻿using FastTests;
using Raven.Abstractions.Indexing;
using Raven.Client.Indexes;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_5927 : RavenTestBase
    {
        private class RavenConflictDocumentsTransformer : AbstractTransformerCreationTask
        {
            public override string TransformerName
            {
                get
                {
                    return "Raven/ConflictDocumentsTransformer";
                }
            }

            public override TransformerDefinition CreateTransformerDefinition(bool prettify = true)
            {
                return new TransformerDefinition
                {
                    Name = TransformerName,
                    TransformResults = @"
from result in results
select new {
    Id = result[""__document_id""],
    ConflictDetectedAt = result[""@metadata""].Value<DateTime>(""Last-Modified""),
                EntityName = result[""@metadata""][""Raven-Entity-Name""],
                Versions = result.Conflicts.Select(versionId =>
                {
                    var version = LoadDocument(versionId);
                    return new
                    {
                        Id = versionId,
                        SourceId = version[""@metadata""][""Raven-Replication-Source""]
                    };
                })
            }
"
                };
            }
        }

        [Fact]
        public void ShouldCompile()
        {
            using (var store = GetDocumentStore())
            {
                new RavenConflictDocumentsTransformer().Execute(store);
            }
        }
    }
}