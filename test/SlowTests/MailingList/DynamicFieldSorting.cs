// -----------------------------------------------------------------------
//  <copyright file="DynamicFieldSorting.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FastTests;
using Raven.Abstractions.Indexing;
using Raven.Client.Indexes;
using Raven.Server.Config;
using Xunit;

namespace SlowTests.MailingList
{
    public class DynamicIndexSort3Specs : RavenTestBase
    {
        private class DataSet
        {
            public string Id { get; set; }
            public List<Item> Items { get; set; }
        }

        private class Item
        {
            public string Id { get; set; }
            public NumericAttribute[] NumericAttributes { get; set; }
            public string SongId { get; set; }
        }

        private class NumericAttribute
        {
            protected NumericAttribute() { }
            public NumericAttribute(string name, double value)
            {
                Name = name;
                Value = value;
            }
            public string Name { get; set; }
            public double Value { get; set; }
        }

        private class WithDynamicIndex :
            AbstractIndexCreationTask<DataSet, WithDynamicIndex.ProjectionItem>
        {
            public class ProjectionItem
            {
                public string SongId { get; set; }
                public NumericAttribute[] NumericAttributes { get; set; }

                public override string ToString()
                {
                    return string.Format("SongId: {0}, N1: {1}", SongId,
                        NumericAttributes.First(x => x.Name == "N1").Value);
                }
            }

            public WithDynamicIndex()
            {
                Map = containers =>
                      from container in containers
                      from item in container.Items
                      select new
                      {
                          SongId = item.SongId,
                          NumericAttributes = item.NumericAttributes,
                          _ = item.NumericAttributes.Select(x => CreateField(x.Name, x.Value))
                      };

                Stores = new Dictionary<Expression<Func<ProjectionItem, object>>, FieldStorage>()
                     {
                         { e=>e.SongId, FieldStorage.Yes},
                         { e=>e.NumericAttributes, FieldStorage.Yes}
                     };
            }
        }

        [Fact]
        public void CanSortDynamically()
        {
            using (var store = GetDocumentStore(modifyDatabaseDocument: document => document.Settings[RavenConfiguration.GetKey(x => x.Indexing.MaxMapIndexOutputsPerDocument)] = "100"))
            {
                new WithDynamicIndex().Execute(store);
                using (var session = store.OpenSession())
                {
                    session.Store(new DataSet
                    {
                        Items = Enumerable.Range(1, 50).Select(x =>
                            new Item
                            {
                                SongId = "songs/" + x,
                                NumericAttributes = new[] { new NumericAttribute("TixP|N1", x * 0.99d) }
                            }).ToList()
                    });

                    session.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    var items = s.Advanced.DocumentQuery<WithDynamicIndex.ProjectionItem, WithDynamicIndex>()
                        .WaitForNonStaleResults()
                        .OrderBy("+TixP|N1_Range")
                        .SelectFields<WithDynamicIndex.ProjectionItem>("SongId", "NumericAttributes")
                        .ToList();
                    Assert.Equal(50, items.Count);
                    var counter = 1;
                    foreach (var item in items)
                    {
                        Assert.Equal("songs/" + counter, item.SongId);
                        counter++;
                    }
                }
            }
        }

        [Fact]
        public void CanSortDynamically_Desc()
        {
            using (var store = GetDocumentStore(modifyDatabaseDocument: document => document.Settings[RavenConfiguration.GetKey(x => x.Indexing.MaxMapIndexOutputsPerDocument)] = "100"))
            {
                new WithDynamicIndex().Execute(store);
                using (var session = store.OpenSession())
                {
                    session.Store(new DataSet
                    {
                        Items = Enumerable.Range(1, 50).Select(x =>
                                    new Item
                                    {
                                        SongId = "songs/" + x,
                                        NumericAttributes = new[]
                                    {
                                        new NumericAttribute("N1",x*0.99d),
                                        new NumericAttribute("N4",x*0.01d),
                                    }
                                    }).ToList()
                    });
                    session.SaveChanges();
                }
                WaitForIndexing(store);
                using (var s = store.OpenSession())
                {
                    var items = s.Advanced.DocumentQuery<WithDynamicIndex.ProjectionItem, WithDynamicIndex>()
                        .WaitForNonStaleResults()
                        .AddOrder("N1_Range", true, typeof(double))
                        .SelectFields<WithDynamicIndex.ProjectionItem>("SongId", "NumericAttributes")
                        .ToList();
                    Assert.Equal(50, items.Count);
                    Assert.Equal("songs/50", items.First().SongId);
                }
            }
        }
    }
}
