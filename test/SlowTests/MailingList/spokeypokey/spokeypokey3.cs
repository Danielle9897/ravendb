using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FastTests;
using Raven.Client.Indexes;
using Xunit;

namespace SlowTests.MailingList.spokeypokey
{
    public class spokeypokey3 : RavenTestBase
    {
        private class ProviderSearchIndex2 : AbstractIndexCreationTask<Provider>
        {
            public ProviderSearchIndex2()
            {
                Map = providers =>
                        from p in providers
                        from c in (p.Categories).DefaultIfEmpty()
                        from po in (p.PracticeOffices).DefaultIfEmpty()
                        select new
                        {
                            p.Name,
                            Categories_Name = c.Name,
                            PracticeOffices_Name = po.Name,
                        };
            }
        }

        [Fact]
        public void Can_deal_with_nulls2()
        {
            using (var documentStore = GetDocumentStore())
            {
                var categories = new List<Category>
                {
                    new Category {Identifier = "123", Name = "SSN"},
                    new Category {Identifier = "345", Name = "EIN"}
                };
                var provider1 = new Provider { };
                // PracticeOffices = null;
                var provider2 = new Provider { Id = "2", Name = "Joe", Categories = categories };
                // Categories is null
                var provider3 = new Provider
                {
                    Id = "3",
                    Name = "Joe",
                    PracticeOffices = new List<PracticeOffice> { new PracticeOffice { Name = "A St. Office" } }
                };
                // PracticeOffice is empty
                var provider4 = new Provider { Id = "4", Name = "Joe", PracticeOffices = new List<PracticeOffice>(), Categories = categories };
                // Categories is empty
                var provider5 = new Provider { Id = "5", Name = "Joe", PracticeOffices = new List<PracticeOffice>(), Categories = new List<Category>() };
                // Both PracticeOffices and Categories have elements.
                var provider6 = new Provider
                {
                    Id = "6",
                    Name = "Joe",
                    PracticeOffices = new List<PracticeOffice> { new PracticeOffice { Name = "A St. Office" } },
                    Categories = categories
                };

                using (var session = documentStore.OpenSession())
                {
                    documentStore.DatabaseCommands.DeleteIndex("ProviderSearchIndex1");
                    documentStore.DatabaseCommands.DeleteIndex("ProviderSearchIndex2");
                    session.Store(provider1);
                    session.Store(provider2);
                    session.Store(provider3);
                    session.Store(provider4);
                    session.Store(provider5);
                    session.Store(provider6);
                    session.SaveChanges();
                }

                // Using autogenerated index
                using (var session = documentStore.OpenSession())
                {
                    var result1 = from p in session.Query<Provider>()
                                  where p.Name == "Joe"
                                  select p;
                    var result1List = result1.ToList();
                    Assert.Equal(5, result1List.Count);

                    var result2 = from p in session.Query<Provider>()
                                  where p.Name == "Joe"
                                  where p.Categories.Any(c => c.Name == "SSN")
                                  select p;
                    var result2List = result2.ToList();
                    Assert.Equal(3, result2List.Count());

                    var result3 = from p in session.Query<Provider>()
                                  where p.Name == "Joe"
                                  where p.Categories.Any(c => c.Name == "SSN")
                                  where p.PracticeOffices.Any(po => po.Name == "A St. Office")
                                  select p;
                    var result3List = result3.ToList();
                    Assert.Equal(1, result3List.Count());
                }

                // Using custom index
                using (var session = documentStore.OpenSession())
                {
                    new ProviderSearchIndex2().Execute(documentStore);

                    var result1 = from p in session.Query<Provider, ProviderSearchIndex2>()
                                  .Customize(x => x.WaitForNonStaleResults())
                                  where p.Name == "Joe"
                                  select p;
                    var result1List = result1.ToList();
                    // Fails here; only Providers 4, 5 and 6 are found.
                    Assert.Equal(5, result1List.Count);

                    var result2 = from p in session.Query<Provider, ProviderSearchIndex2>()
                                   .Customize(x => x.WaitForNonStaleResults())
                                  where p.Name == "Joe"
                                  where p.Categories.Any(c => c.Name == "SSN")
                                  select p;
                    var result2List = result2.ToList();
                    Assert.Equal(3, result2List.Count());

                    var result3 = from p in session.Query<Provider, ProviderSearchIndex2>()
                                  .Customize(x => x.WaitForNonStaleResults())
                                  where p.Name == "Joe"
                                  where p.Categories.Any(c => c.Name == "SSN")
                                  where p.PracticeOffices.Any(po => po.Name == "A St. Office")
                                  select p;


                    var result3List = result3.ToList();
                    Assert.Equal(1, result3List.Count());
                }
            }
        }

        private class Category
        {
            public string Identifier { get; set; }
            public string Name { get; set; }
        }

        private class PracticeOffice
        {
            public string Identifier { get; set; }
            public string ZipCode { get; set; }
            public string Name { get; set; }
        }

        private class Provider
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public IList<PracticeOffice> PracticeOffices { get; set; }
            public IList<Category> Categories { get; set; }
        }

    }
}
