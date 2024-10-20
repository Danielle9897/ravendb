﻿// -----------------------------------------------------------------------
//  <copyright file="Class1.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Raven.NewClient.Client;
using Raven.NewClient.Client.Indexes;
using Xunit;

namespace NewClientTests.NewClient.ResultsTransformer
{
    public class AsyncTransformWith : RavenTestBase
    {
        [Fact]
        public void CanRunTransformerOnSession()
        {
            using (var store = GetDocumentStore())
            {
                store.ExecuteTransformer(new MyTransformer());

                using (var session = store.OpenSession())
                {
                    session.Store(new MyModel
                    {
                        Name = "Sherezade",
                        Country = "India",
                        City = "Delhi"
                    });
                    session.SaveChanges();
                }

                using (var session = store.OpenSession())
                {
                    var model = session.Query<MyModel>()
                        .Search(x => x.Name, "Sherezade")
                        .TransformWith<MyTransformer, MyModelProjection>()
                        .FirstOrDefault();

                    Assert.Equal("Sherezade", model.Name);
                    Assert.Equal("India,Delhi", model.CountryAndCity);
                }
            }
        }

        [Fact]
        public async Task CanRunTransformerOnAsyncSession()
        {
            using (var store = GetDocumentStore())
            {
                store.ExecuteTransformer(new MyTransformer());

                using (var session = store.OpenSession())
                {
                    session.Store(new MyModel
                    {
                        Name = "Sherezade",
                        Country = "India",
                        City = "Delhi"
                    });
                    session.SaveChanges();
                }

                using (var session = store.OpenAsyncSession())
                {
                    var model = await session.Query<MyModel>()
                        .Search(x => x.Name, "Sherezade")
                        .TransformWith<MyTransformer, MyModelProjection>()
                        .FirstOrDefaultAsync();

                    Assert.Equal("Sherezade", model.Name);
                    Assert.Equal("India,Delhi", model.CountryAndCity);
                }
            }
        }

        public class MyModel
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Country { get; set; }
            public string City { get; set; }
        }

        public class MyModelProjection
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string CountryAndCity { get; set; }
        }

        public class MyTransformer : AbstractTransformerCreationTask<MyModel>
        {
            public MyTransformer()
            {
                TransformResults = docus => from d in docus
                                            select new
                                            {
                                                d.Id,
                                                d.Name,
                                                CountryAndCity = String.Join(",", d.Country, d.City)
                                            };
            }
        }
    }
}
