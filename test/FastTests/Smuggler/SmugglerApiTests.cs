using System;
using System.IO;
using System.Threading.Tasks;
using FastTests.Server.Documents.Versioning;
using Raven.Client.Bundles.Versioning;
using Raven.Client.Smuggler;
using Raven.Tests.Core.Utils.Entities;
using Xunit;
using System.Linq;
using System.Threading;
using FastTests.Server.Documents.Expiration;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Abstractions.Indexing;
using Raven.Client.Indexes;
using Raven.Json.Linq;

namespace FastTests.Smuggler
{
    public class SmugglerApiTests : RavenTestBase
    {
        private class Users_ByName : AbstractIndexCreationTask<User>
        {
            public Users_ByName()
            {
                Map = users => from u in users
                               select new
                               {
                                   u.Name
                               };

                Stores.Add(x => x.Name, FieldStorage.Yes);
            }
        }

        private class Users_Address : AbstractTransformerCreationTask<User>
        {
            public Users_Address()
            {
                TransformResults = results => from r in results
                                              let address = LoadDocument<Address>(r.AddressId)
                                              select new
                                              {
                                                  address.City
                                              };
            }
        }

        [Fact]
        public async Task CanExportDirectlyToRemote()
        {
            using (var store1 = GetDocumentStore(dbSuffixIdentifier: "store1"))
            using (var store2 = GetDocumentStore(dbSuffixIdentifier: "store2"))
            {
                using (var session = store1.OpenAsyncSession())
                {
                    await session.StoreAsync(new User { Name = "Name1", LastName = "LastName1" });
                    await session.StoreAsync(new User { Name = "Name2", LastName = "LastName2" });
                    await session.SaveChangesAsync();
                }

                await store1.Smuggler.ExportAsync(new DatabaseSmugglerOptions(), store2.Url, store2.DefaultDatabase);

                var docs = await store2.AsyncDatabaseCommands.GetDocumentsAsync(0, 10);
                Assert.Equal(3, docs.Length);
            }
        }

        [Fact]
        public async Task CanExportAndImport()
        {
            var file = Path.GetTempFileName();
            try
            {
                using (var store1 = GetDocumentStore(dbSuffixIdentifier: "store1"))
                using (var store2 = GetDocumentStore(dbSuffixIdentifier: "store2"))
                {
                    using (var session = store1.OpenSession())
                    {
                        // creating auto-indexes
                        session.Query<User>()
                            .Where(x => x.Age > 10)
                            .ToList();

                        session.Query<User>()
                            .GroupBy(x => x.Name)
                            .Select(x => new { Name = x.Key, Count = x.Count() })
                            .ToList();
                    }

                    new Users_ByName().Execute(store1);
                    new Users_Address().Execute(store1);

                    using (var session = store1.OpenAsyncSession())
                    {
                        await session.StoreAsync(new User { Name = "Name1", LastName = "LastName1" });
                        await session.StoreAsync(new User { Name = "Name2", LastName = "LastName2" });
                        await session.SaveChangesAsync();
                    }

                    await store1.Smuggler.ExportAsync(new DatabaseSmugglerOptions(), file);

                    await store2.Smuggler.ImportAsync(new DatabaseSmugglerOptions(), file);

                    var stats = await store2.AsyncDatabaseCommands.GetStatisticsAsync();
                    Assert.Equal(3, stats.CountOfDocuments);
                    Assert.Equal(3, stats.CountOfIndexes);
                    Assert.Equal(1, stats.CountOfTransformers);
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public async Task SkipExpiredDocumentWhenExport()
        {
            var file = Path.GetTempFileName();
            try
            {
                using (var exportStore = GetDocumentStore(dbSuffixIdentifier: "exportStore"))
                {
                    using (var session = exportStore.OpenAsyncSession())
                    {
                        await Expiration.SetupExpiration(exportStore);
                        var person1 = new Person { Name = "Name1" };
                        await session.StoreAsync(person1).ConfigureAwait(false);
                        var metadata = session.Advanced.GetMetadataFor(person1);
                        metadata[Constants.Expiration.RavenExpirationDate] =
                            new RavenJValue(DateTime.UtcNow.AddSeconds(10)
                                .ToString(Default.DateTimeOffsetFormatsToWrite));

                        await session.SaveChangesAsync().ConfigureAwait(false);
                    }

                    await Task.Delay(10000);
                    await exportStore.Smuggler.ExportAsync(new DatabaseSmugglerOptions { IncludeExpired = false }, file).ConfigureAwait(false);

                }

                using (var importStore = GetDocumentStore(dbSuffixIdentifier: "importStore"))
                {
                    await importStore.Smuggler.ImportAsync(new DatabaseSmugglerOptions(), file);
                    using (var session = importStore.OpenAsyncSession())
                    {
                        var person = await session.LoadAsync<Person>("people/1").ConfigureAwait(false);
                        Assert.Null(person);
                    }
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public async Task CanExportAndImportWithVersioingRevisionDocuments()
        {
            var file = Path.GetTempFileName();
            try
            {
                using (var store1 = GetDocumentStore(dbSuffixIdentifier: "store1"))
                {
                    using (var session = store1.OpenAsyncSession())
                    {
                        await VersioningHelper.SetupVersioning(store1);

                        await session.StoreAsync(new Person { Name = "Name1" });
                        await session.StoreAsync(new Person { Name = "Name2" });
                        await session.StoreAsync(new Company { Name = "Hibernaitng Rhinos " });
                        await session.SaveChangesAsync();
                    }

                    for (int i = 0; i < 2; i++)
                    {
                        using (var session = store1.OpenAsyncSession())
                        {
                            var company = await session.LoadAsync<Company>("companies/1");
                            var person = await session.LoadAsync<Person>("people/1");
                            company.Name += " update " + i;
                            person.Name += " update " + i;
                            await session.StoreAsync(company);
                            await session.StoreAsync(person);
                            await session.SaveChangesAsync();
                        }
                    }

                    using (var session = store1.OpenAsyncSession())
                    {
                        var person = await session.LoadAsync<Person>("people/2");
                        Assert.NotNull(person);
                        session.Delete(person);
                        await session.SaveChangesAsync();
                    }

                    await store1.Smuggler.ExportAsync(new DatabaseSmugglerOptions(), file);

                    var stats = await store1.AsyncDatabaseCommands.GetStatisticsAsync();
                    Assert.Equal(5, stats.CountOfDocuments);
                    Assert.Equal(7, stats.CountOfRevisionDocuments);
                }

                using (var store2 = GetDocumentStore(dbSuffixIdentifier: "store2"))
                {
                    await store2.Smuggler.ImportAsync(new DatabaseSmugglerOptions(), file);

                    var stats = await store2.AsyncDatabaseCommands.GetStatisticsAsync();
                    Assert.Equal(5, stats.CountOfDocuments);
                    Assert.Equal(7, stats.CountOfRevisionDocuments);
                }
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}