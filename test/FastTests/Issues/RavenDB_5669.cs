﻿using System;
using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Client;
using Raven.Client.Indexes;
using Xunit;

namespace FastTests.Issues
{
    public class RavenDB_5669 : RavenTestBase
    {
        [Fact]
        public void WorkingTestWithDifferentSearchTermOrder()
        {
            using (var store = GetDocumentStore())
            {
                store.ExecuteIndex(new Animal_Index());

                StoreAnimals(store);

                using (var session = store.OpenSession())
                {
                    var query = session.Advanced.DocumentQuery<Animal, Animal_Index>();
                    query.OpenSubclause();

                    query = query.WhereEquals(a => a.Type, "Cat");
                    query = query.OrElse();
                    query = query.Search(a => a.Name, "Peter*");
                    query = query.AndAlso();
                    query = query.Search(a => a.Name, "Pan*");

                    query.CloseSubclause();

                    var results = query.ToList();
                    Assert.Equal(1, results.Count);
                }
            }
        }

        [Fact]
        public void WorkingTestWithSubclause()
        {
            using (var store = GetDocumentStore())
            {
                store.ExecuteIndex(new Animal_Index());

                StoreAnimals(store);

                using (var session = store.OpenSession())
                {
                    var query = session.Advanced.DocumentQuery<Animal, Animal_Index>();
                    query.OpenSubclause();

                    query = query.WhereEquals(a => a.Type, "Cat");
                    query = query.OrElse();
                    query = query.OpenSubclause();
                    query = query.Search(a => a.Name, "Pan*");
                    query = query.AndAlso();
                    query = query.Search(a => a.Name, "Peter*");
                    query = query.CloseSubclause();

                    query.CloseSubclause();

                    var results = query.ToList();
                    Assert.Equal(1, results.Count);
                }
            }
        }

        [Fact]
        public void FailingTest()
        {
            using (var store = GetDocumentStore())
            {
                store.ExecuteIndex(new Animal_Index());

                StoreAnimals(store);

                using (var session = store.OpenSession())
                {
                    var query = session.Advanced.DocumentQuery<Animal, Animal_Index>();
                    query.OpenSubclause();

                    query = query.WhereEquals(a => a.Type, "Cat");
                    query = query.OrElse();
                    query = query.Search(a => a.Name, "Pan*");
                    query = query.AndAlso();
                    query = query.Search(a => a.Name, "Peter*");

                    query.CloseSubclause();

                    var results = query.ToList();
                    Assert.Equal(1, results.Count);
                }
            }
        }

        private void StoreAnimals(IDocumentStore store)
        {
            using (var session = store.OpenSession())
            {
                session.Store(new Animal { Name = "Peter Pan", Type = "Dog" });
                session.Store(new Animal { Name = "Peter Poo", Type = "Dog" });
                session.Store(new Animal { Name = "Peter Foo", Type = "Dog" });

                session.SaveChanges();
            }

            WaitForIndexing(store);
        }
        public class Animal
        {
            public string Type { get; set; }
            public string Name { get; set; }
        }

        public class Animal_Index : AbstractIndexCreationTask<Animal>
        {
            public Animal_Index()
            {
                Map = animals => from animal in animals
                                 select new { Name = animal.Name, Type = animal.Type };

                Analyze(a => a.Name, "StandardAnalyzer");
                Index(a => a.Name, FieldIndexing.Analyzed);
            }
        }
    }
}
