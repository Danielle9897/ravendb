﻿using System.Linq;
using Xunit;

namespace NewClientTests.NewClient.Raven.Tests.Bugs.Entities
{
    public class CanSaveUpdateAndRead : RavenTestBase
    {
        [Fact]
        public void Can_read_entity_name_after_update()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    s.Store(new Event { Happy = true });
                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    s.Load<Event>("events/1").Happy = false;
                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    var e = s.Load<Event>("events/1");
                    var entityName = s.Advanced.GetMetadataFor(e)["Raven-Entity-Name"];
                    Assert.Equal("Events", entityName);
                }
            }
        }


        [Fact]
        public void Can_read_entity_name_after_update_from_query()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    s.Store(new Event { Happy = true });
                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    s.Load<Event>("events/1").Happy = false;
                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    var events = s.Query<Event>().Customize(x => x.WaitForNonStaleResults()).ToArray();
                    Assert.NotEmpty(events);
                }
            }
        }

        [Fact]
        public void Can_read_entity_name_after_update_from_query_after_entity_is_in_cache()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    s.Store(new Event { Happy = true });
                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    s.Load<Event>("events/1");//load into cache
                }

                using (var s = store.OpenSession())
                {
                    s.Load<Event>("events/1").Happy = false;
                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    var events = s.Query<Event>().Customize(x => x.WaitForNonStaleResults()).ToArray();
                    Assert.NotEmpty(events);
                }
            }
        }
        public class Event
        {
            public string Id { get; set; }
            public bool Happy { get; set; }
        }
    }
}
