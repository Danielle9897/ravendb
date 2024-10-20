using System;
using System.Collections.Generic;
using System.Linq;
using FastTests;
using Raven.Client.Indexes;
using Xunit;

namespace SlowTests.MailingList
{
    public class TransformerDictionaryOrderTests : RavenTestBase
    {
        [Fact]
        public void CanOrderADictionary()
        {
            using (var store = GetDocumentStore())
            {
                new FooTransformer().Execute(store);

                using (var session = store.OpenSession())
                {
                    session.Store(new Foo { Id = "foos/1", Dates = { { "hello", DateTimeOffset.UtcNow } } });
                    session.SaveChanges();

                    WaitForUserToContinueTheTest(store);
                    var results = session.Load<FooTransformer, FooTransformer.Result>("foos/1");
                    Assert.Equal(1, results.Keys.Count);
                }
            }
        }

        private class Foo
        {
            public string Id { get; set; }
            public Dictionary<string, DateTimeOffset> Dates { get; set; }

            public Foo()
            {
                Dates = new Dictionary<string, DateTimeOffset>();
            }
        }

        private class FooTransformer : AbstractTransformerCreationTask<Foo>
        {
            public class Result
            {
                public List<string> Keys { get; set; }
            }

            public FooTransformer()
            {
                TransformResults = foos => from foo in foos
                                           select new
                                           {
                                               Keys = foo.Dates.OrderBy(x => x.Value).Select(x => x.Key).ToList()
                                           };
            }
        }
    }
}
