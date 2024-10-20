﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NewClientTests.NewClient.Server.Replication
{
    public class ReplicaionWriteAssurance: ReplicationTestsBase
    {
        [Fact]
        public void ServerSideWriteAssurance()
        {
            var store1 = GetDocumentStore(dbSuffixIdentifier: "dbName1");
            var store2 = GetDocumentStore(dbSuffixIdentifier: "dbName2");
            var store3 = GetDocumentStore(dbSuffixIdentifier: "dbName3");

            SetupReplication(store1, store2, store3);

            using (var s1 = store1.OpenSession())
            {
                s1.Advanced.WaitForReplicationAfterSaveChanges(replicas: 2, timeout: TimeSpan.FromSeconds(30));

                s1.Store(new { Name = "Idan" }, "users/1");

                s1.SaveChanges();
            }

            using (var s1 = store1.OpenSession())
            {
                Assert.NotNull(s1.Load<dynamic>("users/1"));
            }

            using (var s2 = store2.OpenSession())
            {
                var s = s2.Load<dynamic>("users/1");
                Assert.NotNull(s);
            }

            using (var s3 = store3.OpenSession())
            {
                Assert.NotNull(s3.Load<dynamic>("users/1"));
            }
        }
    }
}
