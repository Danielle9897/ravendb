using System.Linq;
using FastTests;
using Raven.Client.Documents.Indexes;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Issues
{
    public class RavenDB_5151 : RavenTestBase
    {
        public RavenDB_5151(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void CanDoPrefixQueryOnAnalyzedFields()
        {
            using (var store = GetDocumentStore())
            using (var session = store.OpenSession())
            {
                new FooBarIndex().Execute(store);
                session.Store(new Foo { Bar = "Shalom111" });
                session.Store(new Foo { Bar = "Salam222" });
                session.SaveChanges();
                WaitForIndexing(store);

                WaitForUserToContinueTheTest(store);

                var res = session.Query<Foo, FooBarIndex>().Single(x => x.Bar.StartsWith("Sh"));
                Assert.Equal(res.Bar, "Shalom");
            }
        }

        private class Foo
        {
            public string Bar { get; set; }
        }

        private class FooBarIndex : AbstractIndexCreationTask<Foo>
        {
            public FooBarIndex()
            {
                Map = foos => from foo in foos select new { foo.Bar };
                //Analyzers.Add(c => c.Bar, typeof(Lucene.Net.Analysis.Standard.StandardAnalyzer).ToString());

                Analyzers.Add(c => c.Bar, "WhitespaceAnalyzer");

                //Index("Bar", FieldIndexing.Search);
                //Index("__all_fields", FieldIndexing.Exact);
            }
        }
    }
}
