// -----------------------------------------------------------------------
//  <copyright file="ReferencedDocuments.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using FastTests;
using Raven.Abstractions.Data;
using Raven.Client.Indexes;
using Raven.Client.Indexing;
using SlowTests.Core.Utils.Entities;
using SlowTests.Core.Utils.Indexes;
using SlowTests.Core.Utils.Transformers;

using Xunit;

using Company = SlowTests.Core.Utils.Entities.Company;
using Employee = SlowTests.Core.Utils.Entities.Employee;
using Post = SlowTests.Core.Utils.Entities.Post;
using PostContent = SlowTests.Core.Utils.Entities.PostContent;

namespace SlowTests.Core.Indexing
{
    public class ReferencedDocuments : RavenTestBase
    {
        [Fact]
        public void CanUseLoadDocumentToIndexReferencedDocs()
        {
            using (var store = GetDocumentStore())
            {
                var postsByContent = new Posts_ByContent();
                postsByContent.Execute(store);

                var companiesWithEmployees = new Companies_WithReferencedEmployees();
                companiesWithEmployees.Execute(store);

                var companiesWithEmployeesTransformer = new CompanyEmployeesTransformer();
                companiesWithEmployeesTransformer.Execute(store);

                using (var session = store.OpenSession())
                {
                    for (int i = 0; i < 10; i++)
                    {
                        session.Store(new Post
                        {
                            Id = "posts/" + i
                        });

                        session.Store(new PostContent
                        {
                            Id = "posts/" + i + "/content",
                            Text = i % 2 == 0 ? "HTML 5" : "Javascript"
                        });

                        session.Store(new Employee
                        {
                            Id = "employees/" + i,
                            LastName = "Last Name " + i
                        });
                    }

                    session.Store(new Company { EmployeesIds = new List<string>() { "employees/1", "employees/2", "employees/3" } });
                    session.SaveChanges();
                    WaitForIndexing(store);

                    var html5PostsQuery = session.Advanced.DocumentQuery<Post>(postsByContent.IndexName).WhereEquals("Text", "HTML 5");
                    var javascriptPostsQuery = session.Advanced.DocumentQuery<Post>(postsByContent.IndexName).WhereEquals("Text", "Javascript");

                    Assert.Equal(5, html5PostsQuery.ToList().Count);
                    Assert.Equal(5, javascriptPostsQuery.ToList().Count);


                    var companies = session.Advanced.DocumentQuery<Companies_WithReferencedEmployees.CompanyEmployees>(companiesWithEmployees.IndexName)
                        .SetResultTransformer(companiesWithEmployeesTransformer.TransformerName)
                        .ToArray();

                    Assert.Equal(1, companies.Length);
                    Assert.Equal("Last Name 1", companies[0].Employees[0]);
                    Assert.Equal("Last Name 2", companies[0].Employees[1]);
                    Assert.Equal("Last Name 3", companies[0].Employees[2]);
                }
            }
        }

        [Fact]
        public void BasicLoadDocumentsWithEnumerable()
        {
            using (var store = GetDocumentStore())
            {
                new Companies_ByEmployeeLastName().Execute(store);

                using (var session = store.OpenSession())
                {
                    var employee1 = new Employee { LastName = "Doe" };
                    var employee2 = new Employee { LastName = "Gates" };

                    session.Store(employee1);
                    session.Store(employee2);

                    var company = new Company
                    {
                        Name = "HR",
                        EmployeesIds = new List<string>
                        {
                            employee1.Id,
                            employee2.Id
                        }
                    };

                    session.Store(company);

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var companies = session.Query<Companies_ByEmployeeLastName.Result, Companies_ByEmployeeLastName>()
                        .Where(x => x.LastName == "Gates")
                        .OfType<Company>()
                        .ToList();

                    Assert.Equal(1, companies.Count);
                    Assert.Equal("HR", companies[0].Name);
                }
            }
        }

        [Fact]
        public void BasicLoadDocuments()
        {
            using (var store = GetDocumentStore())
            {
                new Users_ByCity().Execute(store);

                using (var session = store.OpenSession())
                {
                    var address1 = new Address { City = "New York" };
                    var address2 = new Address { City = "Warsaw" };

                    session.Store(address1);
                    session.Store(address2);

                    var user1 = new User
                    {
                        LastName = "Doe",
                        AddressId = address1.Id
                    };

                    var user2 = new User
                    {
                        LastName = "Nowak",
                        AddressId = address2.Id
                    };

                    session.Store(user1);
                    session.Store(user2);

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var users = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Where(x => x.City == "New York")
                        .OfType<User>()
                        .ToList();

                    Assert.Equal(1, users.Count);
                    Assert.Equal("Doe", users[0].LastName);

                    var count = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Count();

                    Assert.Equal(2, count);
                }

                using (var session = store.OpenSession())
                {
                    var address = session.Load<Address>("addresses/1");
                    address.City = "Barcelona";

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var users = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Where(x => x.City == "New York")
                        .OfType<User>()
                        .ToList();

                    Assert.Equal(0, users.Count);

                    users = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Where(x => x.City == "Barcelona")
                        .OfType<User>()
                        .ToList();

                    Assert.Equal(1, users.Count);
                    Assert.Equal("Doe", users[0].LastName);
                }

                using (var session = store.OpenSession())
                {
                    session.Delete("addresses/1");

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    // address from LoadDocument will be null so the City value will not get into index
                    // we cannot expect to return any users here in that case
                    var users = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Where(x => x.City == null)
                        .OfType<User>()
                        .ToList();

                    Assert.Equal(0, users.Count);
                }

                using (var session = store.OpenSession())
                {
                    var user1 = session.Load<User>("users/1");
                    user1.AddressId = "addresses/2";

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var users = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Where(x => x.City == "Warsaw")
                        .OfType<User>()
                        .ToList();

                    Assert.Equal(2, users.Count);
                }

                using (var session = store.OpenSession())
                {
                    session.Delete("users/1");

                    session.SaveChanges();
                }

                WaitForIndexing(store);
            }
        }

        [Fact]
        public void BasicLoadDocuments_Casing()
        {
            using (var store = GetDocumentStore())
            {
                store.DatabaseCommands.PutIndex("Users/ByCity", new IndexDefinition
                {
                    Maps =
                    {
                        @"from user in docs.Users
                               let address1 = LoadDocument(user.AddressId, ""addresses"")
                               let address2 = LoadDocument(user.AddressId, ""Addresses"")
                               select new
                               {
                                   City = address1.City
                               }"
                    }
                });

                using (var session = store.OpenSession())
                {
                    var address1 = new Address { City = "New York" };
                    var address2 = new Address { City = "Warsaw" };

                    session.Store(address1);
                    session.Store(address2);

                    session.Advanced.GetMetadataFor(address2)[Constants.Headers.RavenEntityName] = "addresses";

                    var user1 = new User
                    {
                        LastName = "Doe",
                        AddressId = address1.Id
                    };

                    var user2 = new User
                    {
                        LastName = "Nowak",
                        AddressId = address2.Id
                    };

                    session.Store(user1);
                    session.Store(user2);

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var users = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Where(x => x.City == "New York")
                        .OfType<User>()
                        .ToList();

                    Assert.Equal(1, users.Count);
                    Assert.Equal("Doe", users[0].LastName);

                    var count = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Count();

                    Assert.Equal(2, count);
                }

                using (var session = store.OpenSession())
                {
                    var address = session.Load<Address>("addresses/1");
                    address.City = "Barcelona";

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var users = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Where(x => x.City == "New York")
                        .OfType<User>()
                        .ToList();

                    Assert.Equal(0, users.Count);

                    users = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Where(x => x.City == "Barcelona")
                        .OfType<User>()
                        .ToList();

                    Assert.Equal(1, users.Count);
                    Assert.Equal("Doe", users[0].LastName);
                }

                using (var session = store.OpenSession())
                {
                    session.Delete("addresses/1");

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    // address from LoadDocument will be null so the City value will not get into index
                    // we cannot expect to return any users here in that case
                    var users = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Where(x => x.City == null)
                        .OfType<User>()
                        .ToList();

                    Assert.Equal(0, users.Count);
                }

                using (var session = store.OpenSession())
                {
                    var user1 = session.Load<User>("users/1");
                    user1.AddressId = "addresses/2";

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var users = session.Query<Users_ByCity.Result, Users_ByCity>()
                        .Where(x => x.City == "Warsaw")
                        .OfType<User>()
                        .ToList();

                    Assert.Equal(2, users.Count);
                }

                using (var session = store.OpenSession())
                {
                    session.Delete("users/1");

                    session.SaveChanges();
                }

                WaitForIndexing(store);
            }
        }

        [Fact]
        public void ShouldReindexOnReferencedDocumentChange()
        {
            using (var store = GetDocumentStore())
            {
                var postsByContent = new Posts_ByContent();
                postsByContent.Execute(store);

                using (var session = store.OpenSession())
                {
                    PostContent last = null;
                    for (int i = 0; i < 3; i++)
                    {
                        session.Store(new Post
                        {
                            Id = "posts/" + i
                        });

                        session.Store(last = new PostContent
                        {
                            Id = "posts/" + i + "/content",
                            Text = i % 2 == 0 ? "HTML 5" : "Javascript"
                        });
                    }

                    session.SaveChanges();
                    WaitForIndexing(store);

                    Assert.Equal(2, session.Advanced.DocumentQuery<Post>(postsByContent.IndexName).WhereEquals("Text", "HTML 5").ToList().Count);
                    Assert.Equal(1, session.Advanced.DocumentQuery<Post>(postsByContent.IndexName).WhereEquals("Text", "Javascript").ToList().Count);

                    last.Text = "JSON"; // referenced document change

                    session.Store(last);

                    session.SaveChanges();
                    WaitForIndexing(store);

                    Assert.Equal(1, session.Advanced.DocumentQuery<Post>(postsByContent.IndexName).WhereEquals("Text", "HTML 5").ToList().Count);
                    Assert.Equal(1, session.Advanced.DocumentQuery<Post>(postsByContent.IndexName).WhereEquals("Text", "Javascript").ToList().Count);
                    Assert.Equal(1, session.Advanced.DocumentQuery<Post>(postsByContent.IndexName).WhereEquals("Text", "JSON").ToList().Count);

                    session.Delete(last); // referenced document delete

                    session.SaveChanges();
                    WaitForIndexing(store);

                    Assert.Equal(0, session.Advanced.DocumentQuery<Post>(postsByContent.IndexName).WhereEquals("Text", "JSON").ToList().Count);
                }
            }
        }

        [Fact]
        public void CanProceedWhenReferencedDocumentsAreMissing()
        {
            using (var store = GetDocumentStore())
            {
                var postsByContent = new Posts_ByContent();
                postsByContent.Execute(store);

                using (var session = store.OpenSession())
                {
                    for (int i = 0; i < 10; i++)
                    {
                        session.Store(new Post
                        {
                            Id = "posts/" + i
                        });

                        if (i % 2 == 0)
                        {
                            session.Store(new PostContent
                            {
                                Id = "posts/" + i + "/content",
                                Text = "HTML 5"
                            });
                        }
                    }

                    session.SaveChanges();
                    WaitForIndexing(store);

                    Assert.Equal(5, session.Advanced.DocumentQuery<Post>(postsByContent.IndexName).WhereEquals("Text", null).ToList().Count);
                }
            }
        }
    }
}
