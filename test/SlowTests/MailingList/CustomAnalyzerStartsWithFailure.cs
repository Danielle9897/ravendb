using System.Linq;
using System.Threading.Tasks;
using FastTests;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Raven.Abstractions.Indexing;
using Raven.Client;
using Raven.Client.Indexes;
using Xunit;

namespace SlowTests.MailingList
{
    public class CustomAnalyzerStartsWithFailure : RavenTestBase
    {
        public void Fill(IDocumentStore store)
        {
            store.ExecuteIndex(new CustomerByName());

            using (var session = store.OpenSession())
            {
                session.Store(new Customer() { Name = "Rog�rio" });
                session.Store(new Customer() { Name = "Rogerio" });
                session.Store(new Customer() { Name = "Paulo Rogerio" });
                session.Store(new Customer() { Name = "Paulo Rog�rio" });
                session.SaveChanges();
            }
        }

        [Fact]
        public void query_customanalyzer_with_equals()
        {
            using (var store = GetDocumentStore())
            {
                Fill(store);

                using (IDocumentSession session = store.OpenSession())
                {
                    // Test 1
                    // Using "== Rog�rio" works fine
                    var results1 = session.Query<Customer, CustomerByName>()
                        .Customize(x => x.WaitForNonStaleResults())
                        .Where(x => x.Name == "Rog�rio");

                    Assert.Equal(results1.Count<Customer>(), 4);
                }
            }
        }

        [Fact]
        public void query_customanalyzer_with_starswith()
        {
            using (var store = GetDocumentStore())
            {
                Fill(store);
                using (IDocumentSession session = store.OpenSession())
                {
                    WaitForUserToContinueTheTest(store);
                    // Test 2
                    // Using ".StartsWith("Rog�rio")" is expected to bring same result from test1, but fails
                    var results2 = session.Query<Customer, CustomerByName>()
                        .Customize(x => x.WaitForNonStaleResults())
                        .Where(x => x.Name.StartsWith("Rog�rio"));

                    Assert.Equal(results2.Count<Customer>(), 4);
                }
            }
        }

        private class Customer
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        private class CustomerByName : AbstractIndexCreationTask<Customer>
        {
            public CustomerByName()
            {
                Map = customers => from customer in customers select new { customer.Name };
                Indexes.Add(x => x.Name, FieldIndexing.Analyzed);
                Analyzers.Add(x => x.Name, typeof(CustomAnalyzer).AssemblyQualifiedName);
            }
        }

        private class CustomAnalyzer : StandardAnalyzer
        {
            public CustomAnalyzer()
                : base(Lucene.Net.Util.Version.LUCENE_30)
            {
            }

            public override TokenStream TokenStream(string fieldName, System.IO.TextReader reader)
            {
                return new ASCIIFoldingFilter(base.TokenStream(fieldName, reader));
            }
        }

    }
}
