// -----------------------------------------------------------------------
//  <copyright file="User.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------
namespace Raven.Tests.Core.Utils.Entities
{
    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public string AddressId { get; set; }
        public int Count { get; set; }
        public int Age { get; set; }
    }

    public class Address
    {
        public string Id { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Street { get; set; }
        public int ZipCode { get; set; }
    }

    public class Person
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string AddressId { get; set; }
    }

    public class PersonWithAddress
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public Address Address { get; set; }
    }

}
