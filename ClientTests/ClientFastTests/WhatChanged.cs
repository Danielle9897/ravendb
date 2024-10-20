﻿using Raven.NewClient.Abstractions.Data;
using Xunit;

namespace NewClientTests.NewClient
{
    public class WhatChanged : RavenTestBase
    {
        [Fact]
        public void What_Changed_New_Field()
        {
            using (var store = GetDocumentStore())
            {
                using (var newSession = store.OpenSession())
                {
                    newSession.Store(new BasicName()
                    {
                        Name = "Toli"
                    } 
                    , "users/1");
                  
                    Assert.Equal(newSession.Advanced.WhatChanged().Count, 1);
                    newSession.SaveChanges();
                }
                using (var newSession = store.OpenSession())
                {
                    var user = newSession.Load<NameAndAge>("users/1");
                    user.Age = 5;
                    var changes = newSession.Advanced.WhatChanged();
                    Assert.Equal(changes["users/1"].Length, 1);
                    Assert.Equal(changes["users/1"][0].Change, DocumentsChanges.ChangeType.NewField);
                    newSession.SaveChanges();
                }
            }
        }

        [Fact]
        public void What_Changed_Removed_Field()
        {
            using (var store = GetDocumentStore())
            {
                using (var newSession = store.OpenSession())
                {
                    newSession.Store(new NameAndAge()
                    {
                        Name = "Toli",
                        Age = 5
                    }
                    , "users/1");

                    Assert.Equal(newSession.Advanced.WhatChanged().Count, 1);
                    newSession.SaveChanges();
                }

                using (var newSession = store.OpenSession())
                {
                    newSession.Load<BasicAge>("users/1");
                    var changes = newSession.Advanced.WhatChanged();
                    Assert.Equal(changes["users/1"].Length, 1);
                    Assert.Equal(changes["users/1"][0].Change, DocumentsChanges.ChangeType.RemovedField);
                    newSession.SaveChanges();
                }
            }
        }

        [Fact]
        public void What_Changed_Change_Field()
        {
            using (var store = GetDocumentStore())
            {
                using (var newSession = store.OpenSession())
                {
                    newSession.Store(new BasicAge()
                    {
                        Age = 5
                    }
                    , "users/1");

                    Assert.Equal(newSession.Advanced.WhatChanged().Count, 1);
                    newSession.SaveChanges();
                }

                using (var newSession = store.OpenSession())
                {
                    newSession.Load<Number>("users/1");
                    var changes = newSession.Advanced.WhatChanged();
                    Assert.Equal(changes["users/1"].Length, 2);
                    Assert.Equal(changes["users/1"][0].Change, DocumentsChanges.ChangeType.RemovedField);
                    Assert.Equal(changes["users/1"][1].Change, DocumentsChanges.ChangeType.NewField);
                    newSession.SaveChanges();
                }
            }
        }
    }

    public class BasicName
    {
        public string Name { set; get; }
    }

    public class NameAndAge
    {
        public string Name { set; get; }
        public int Age { set; get; }
    }

    public class BasicAge
    {
        public int Age { set; get; }
    }

    public class Number
    {
        public int Num { set; get; }
    }
}
