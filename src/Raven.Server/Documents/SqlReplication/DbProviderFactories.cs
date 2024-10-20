using System;
using System.Data.Common;
using System.Data.SqlClient;
using Npgsql;
namespace Raven.Server.Documents.SqlReplication
{
    public class DbProviderFactories
    {
        public static DbProviderFactory GetFactory(string factoryName)
        {
            switch (factoryName)
            {
                case "System.Data.SqlClient":
                    return SqlClientFactory.Instance;
                case "Npgsql":
                    return Npgsql.NpgsqlFactory.Instance;
                case "System.Data.SqlServerCe.4.0":
                case "System.Data.OleDb":
                case "System.Data.OracleClient":
                case "MySql.Data.MySqlClient":
                case "System.Data.SqlServerCe.3.5":

                    throw new NotImplementedException($"Factory '{factoryName}' is not implemented yet");
                default:
                    throw new NotSupportedException($"Factory '{factoryName}' is not supported");
            }
        }        
    }

    public static class DbProviderFactoryExtensions
    {
        public static DbCommandBuilder CreateCommandBuilder(this DbProviderFactory factory)
        {
            if (factory is SqlClientFactory)
                return new DbCommandBuilder
                {
                    Start = "[",
                    End = "]"
                };
            if (factory is NpgsqlFactory)
                return new DbCommandBuilder
                {
                    Start = "\"",
                    End = "\""
                };
            return new DbCommandBuilder
            {
            };
        }
    }
}