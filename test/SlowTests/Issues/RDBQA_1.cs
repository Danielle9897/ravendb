// -----------------------------------------------------------------------
//  <copyright file="RDBQA_1.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using FastTests;
using Raven.Abstractions.Connection;
using Xunit;

namespace SlowTests.Issues
{
    public class RDBQA_1 : RavenTestBase
    {
        private class Doc
        {
            public string Id { get; set; }

            public string Name { get; set; }
        }

        [Fact(Skip = "RavenDB-5912")]
        public void InsertingAndReadingDocumentWithReadOnlyFlagShouldWork()
        {
            using (var store = GetDocumentStore())
            {
                string docId;
                using (var session = store.OpenSession())
                {
                    var doc = new Doc { Name = "Name1" };
                    session.Store(doc);
                    //session.Advanced.GetMetadataFor(doc)[Constants.RavenReadOnly] = true;

                    session.SaveChanges();

                    docId = doc.Id;
                }

                using (var session = store.OpenSession())
                {
                    var doc = session.Load<Doc>(docId);

                    Assert.NotNull(doc);
                    Assert.Equal("Name1", doc.Name);
                }
            }
        }

        [Fact(Skip = "RavenDB-5912")]
        public void UpdatingAndDeletingDocumentWithReadOnlyFlagShouldThrow()
        {
            using (var store = GetDocumentStore())
            {
                string docId;
                using (var session = store.OpenSession())
                {
                    var doc = new Doc { Name = "Name1" };
                    session.Store(doc);
                    //session.Advanced.GetMetadataFor(doc)[Constants.RavenReadOnly] = true;

                    session.SaveChanges();

                    docId = doc.Id;
                }

                using (var session = store.OpenSession())
                {
                    var doc = session.Load<Doc>(docId);

                    var e1 = Assert.Throws<InvalidOperationException>(() => session.Delete(doc));
                    Assert.Equal("Raven.Tests.Issues.RDBQA_1+Doc is marked as read only and cannot be deleted", e1.Message);

                    session.Advanced.Clear();

                    session.Delete(docId);
                    var e2 = Assert.Throws<ErrorResponseException>(() => session.SaveChanges());
                    Assert.Contains("DELETE vetoed on document docs/1 by Raven.Database.Plugins.Builtins.ReadOnlyDeleteTrigger because: You cannot delete document 'docs/1' because it is marked as readonly. Consider changing 'Raven-Read-Only' flag to 'False'.", e2.Message);
                }

                using (var session = store.OpenSession())
                {
                    var doc = session.Load<Doc>(docId);
                    doc.Name = "Name2";

                    session.Store(doc);
                    session.SaveChanges();

                    session.Advanced.Clear();

                    doc = session.Load<Doc>(docId);
                    Assert.Equal("Name1", doc.Name);

                    session.Advanced.Clear();

                    doc.Name = "Name2";
                    session.Store(doc);
                    //session.Advanced.GetMetadataFor(doc)[Constants.RavenReadOnly] = true;

                    var e = Assert.Throws<ErrorResponseException>(() => session.SaveChanges());
                    Assert.Contains("PUT vetoed on document docs/1 by Raven.Database.Plugins.Builtins.ReadOnlyPutTrigger because: You cannot update document 'docs/1' when both of them, new and existing one, are marked as readonly. To update this document change 'Raven-Read-Only' flag to 'False' or remove it entirely.", e.Message);
                }
            }
        }

        [Fact(Skip = "RavenDB-5912")]
        public void CanUpdateDocumentWhenReadOnlyFlagChanged()
        {
            using (var store = GetDocumentStore())
            {
                string docId;
                using (var session = store.OpenSession())
                {
                    var doc = new Doc { Name = "Name1" };
                    session.Store(doc);
                    //session.Advanced.GetMetadataFor(doc)[Constants.RavenReadOnly] = true;

                    session.SaveChanges();

                    docId = doc.Id;
                }

                using (var session = store.OpenSession())
                {
                    var doc = new Doc { Id = docId, Name = "Name1" };
                    session.Store(doc);
                    //session.Advanced.GetMetadataFor(doc)[Constants.RavenReadOnly] = false;

                    session.SaveChanges();
                    session.Advanced.Clear();

                    doc = session.Load<Doc>(docId);
                    var metadata = session.Advanced.GetMetadataFor(doc);
                    //Assert.False(metadata.Value<bool>(Constants.RavenReadOnly));

                    //metadata.Remove(Constants.RavenReadOnly);

                    session.SaveChanges();
                    session.Advanced.Clear();

                    doc = session.Load<Doc>(docId);
                    metadata = session.Advanced.GetMetadataFor(doc);
                    //Assert.False(metadata.ContainsKey(Constants.RavenReadOnly));
                }
            }
        }
    }
}
