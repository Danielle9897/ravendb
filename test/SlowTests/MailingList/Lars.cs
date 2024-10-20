// -----------------------------------------------------------------------
//  <copyright file="Lars.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using FastTests;
using Raven.Client.Indexes;
using Xunit;

namespace SlowTests.MailingList
{
    public class Lars : RavenTestBase
    {
        private class Item
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        private class Index : AbstractIndexCreationTask<Item, Index.Result>
        {
            public class Result
            {
                public int Age { get; set; }
            }

            public Index()
            {
                Map = items =>
                      from item in items
                      select new
                      {
                          item.Age
                      };

                Reduce = results =>
                         from r in results
                         group r by 1
                         into g
                         let items = g.ToArray()
                         select new { Age = items.Sum(x => x.Age) };
            }
        }

        [Fact]
        public void EnumerableMethodsShouldBeExternalStaticCalls()
        {
            using (var s = GetDocumentStore())
            {
                new Index().Execute(s);
                var indexDefinition = s.DatabaseCommands.GetIndex("Index");
                Assert.Contains("Enumerable.ToArray(g)", indexDefinition.Reduce);
                Assert.Contains("Enumerable.Sum", indexDefinition.Reduce);
            }
        }
    }
}
