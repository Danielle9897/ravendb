﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Abstractions.Logging;
using Raven.Abstractions.Replication;
using Raven.Abstractions.Util;
using Raven.Client;
using Raven.Client.Connection.Async;
using Raven.Client.Connection.Request;
using Raven.Client.Data;
using Raven.Client.Document;
using Raven.Client.Extensions;
using Raven.Json.Linq;
using Raven.Traffic;


namespace TrafficRecorder
{

    public class Program
    {
        private static void Main(string[] args)
        {
            TrafficToolConfiguration config;
            var parseStatus = TrafficToolConfiguration.ProcessArgs(args, out config);
            switch (parseStatus)
            {
                case TrafficToolConfiguration.TrafficArgsProcessStatus.InvalidMode:
                    PrintUsage();
                    break;
                case TrafficToolConfiguration.TrafficArgsProcessStatus.NoArguments:
                    PrintUsage();
                    break;
                case TrafficToolConfiguration.TrafficArgsProcessStatus.NotEnoughArguments:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Illegal arguments amount, see usage instructions:");
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case TrafficToolConfiguration.TrafficArgsProcessStatus.ValidConfig:
                    IDocumentStore store;
                    try
                    {
                        store = new DocumentStore
                        {
                            Url = config.ConnectionString.Url,
                            DefaultDatabase = config.ResourceName,
                            Credentials = config.ConnectionString.Credentials,
                        }.Initialize();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Could not connect to server. Exception: {0}",e);
                        return;
                    }

                    using (store)
                    {
                        try
                        {
                            store.DatabaseCommands.GetStatistics();
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Database does not exist");
                            return;
                        }
                        new TrafficRec(store, config).ExecuteTrafficCommand();
                    }
                    break;
            }
            
            
        }

        private static void PrintUsage()
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine(@"
Traffic Recording and Replaying utility for RavenDB
----------------------------------------------
Copyright (C) 2008 - {0} - Hibernating Rhinos
----------------------------------------------", SystemTime.UtcNow.Year);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(@"
Usage:
    Raven.Traffic [Mode(rec/play)] [Url] [resource name] [recordingFile] [[option1], [option2] ...] 

Examples:
  - Record 'Northwind' database found on specified server:
    Raven.Traffic rec http://localhost:8080/ Northwind  recording.json
  - Replay 'Northwind' database from specified server to the dump.raven file:
    Raven.Traffic play http://localhost:8080/ Northwind recording.json ");

            Console.ForegroundColor = ConsoleColor.Green;
            TrafficToolConfiguration.InitOptionsSetObject().WriteOptionDescriptions(Console.Out);
            Console.ForegroundColor = ConsoleColor.White;
        }

        
    }
}
