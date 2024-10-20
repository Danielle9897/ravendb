// -----------------------------------------------------------------------
//  <copyright file="CanQueryOnCustomClass.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;

using FastTests;

using Raven.Imports.Newtonsoft.Json;

using Xunit;

namespace SlowTests.Tests.Queries
{
    public class CanQueryOnCustomClass : RavenTestBase
    {
        [Fact]
        public void UsingConverter()
        {
            using (var store = GetDocumentStore())
            {
                store.Conventions.CustomizeJsonSerializer += x => x.Converters = new JsonConverterCollection(x.Converters) { new MoneyConverter() };

                using (var session = store.OpenSession())
                {
                    session.Store(new Order
                    {
                        Value = new Money
                        {
                            Currency = "$",
                            Amount = 50
                        }
                    });
                    session.SaveChanges();
                }

                using (var session = store.OpenSession())
                {
                    var q = session.Query<Order>().Where(x => x.Value == new Money
                    {
                        Currency = "$",
                        Amount = 50
                    });
                    Assert.Equal(@"Value:$\:50", q.ToString());
                    var orders = q.ToList();

                    Assert.NotEmpty(orders);
                }
            }
        }

        public class Order
        {
            public Money Value;
        }

        public class Money
        {
            public string Currency;
            public decimal Amount;
        }

        public class MoneyConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var m = ((Money)value);
                writer.WriteValue(m.Currency + ":" + m.Amount);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var readAsString = reader.Value.ToString();
                var strings = readAsString.Split(':');
                return new Money
                {
                    Currency = strings[0],
                    Amount = decimal.Parse(strings[1])
                };
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Money);
            }
        }
    }
}
