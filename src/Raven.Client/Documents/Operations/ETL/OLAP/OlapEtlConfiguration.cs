﻿using System;
using System.Collections.Generic;
using System.Linq;
using Sparrow.Json.Parsing;

namespace Raven.Client.Documents.Operations.ETL.OLAP
{
    public class OlapEtlConfiguration : EtlConfiguration<OlapConnectionString>
    {
        public string RunFrequency { get; set; }

        public OlapEtlFileFormat Format { get; set; }

        public string CustomField { get; set; }

        public List<OlapEtlTable> OlapTables { get; set; }

        private string _name;

        public override string GetDestination()
        {
            return _name ??= Connection.GetDestination();
        }

        public override EtlType EtlType => EtlType.Olap;

        public override bool UsingEncryptedCommunicationChannel()
        {
            //RavenDB - 16627
            throw new NotImplementedException();
        }

        public override string GetDefaultTaskName()
        {
            return $"OLAP ETL to {ConnectionStringName}";
        }
        
        public override DynamicJsonValue ToJson()
        {
            var json = base.ToJson();

            json[nameof(CustomField)] = CustomField;
            json[nameof(RunFrequency)] = RunFrequency;
            json[nameof(OlapTables)] = new DynamicJsonArray(OlapTables.Select(x => x.ToJson()));

            return json;
        }
    }

    public class OlapEtlTable
    {
        public string TableName { get; set; }

        public string DocumentIdColumn { get; set; }

        protected bool Equals(OlapEtlTable other)
        {
            return string.Equals(TableName, other.TableName) && string.Equals(DocumentIdColumn, other.DocumentIdColumn, StringComparison.OrdinalIgnoreCase);
        }

        public DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(TableName)] = TableName,
                [nameof(DocumentIdColumn)] = DocumentIdColumn
            };
        }
    }
}