// -----------------------------------------------------------------------
//  <copyright file="Sean.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using FastTests;
using Raven.Abstractions.Data;
using Raven.Client.Data;
using Xunit;

namespace SlowTests.MailingList
{
    public class Sean : RavenTestBase
    {
        private class Thread
        {
            public DateTime? CreationDate { get; set; }
        }

        [Fact]
        public void CanUseNullablesForFacets()
        {
            Facet facet = new Facet<Thread>
            {
                Name = t => t.CreationDate,
                Ranges =
                {
                    t => t.CreationDate.Value < new DateTime(2012, 1, 1),
                }
            };

            Assert.Equal(@"[NULL TO 2012\-01\-01T00\:00\:00.0000000]", facet.Ranges[0]);
        }
    }
}
