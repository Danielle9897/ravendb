﻿using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Configuration;

using Raven.Server.Config.Attributes;
using Raven.Server.Config.Categories;
using ExpressionExtensions = Raven.Server.Extensions.ExpressionExtensions;
using Sparrow;
using Sparrow.Logging;
using Sparrow.Platform;

namespace Raven.Server.Config
{
    public class RavenConfiguration
    {
        private readonly IConfigurationBuilder _configBuilder;

        public bool Initialized { get; private set; }

        public CoreConfiguration Core { get; }

        public SqlReplicationConfiguration SqlReplication { get; }

        public ReplicationConfiguration Replication { get; }

        public StorageConfiguration Storage { get; }

        public EncryptionConfiguration Encryption { get; }

        public IndexingConfiguration Indexing { get; set; }

        public MonitoringConfiguration Monitoring { get; }

        public WebSocketsConfiguration WebSockets { get; set; }

        public QueryConfiguration Queries { get; }

        public PatchingConfiguration Patching { get; }

        public BulkInsertConfiguration BulkInsert { get; }

        public ServerConfiguration Server { get; }

        public MemoryConfiguration Memory { get; }

        public ExpirationBundleConfiguration Expiration { get; }

        public StudioConfiguration Studio { get; }

        public DatabaseConfiguration Databases { get; }

        public LicenseConfiguration Licensing { get; }

        public QuotasBundleConfiguration Quotas { get; }

        public TombstoneConfiguration Tombstones { get; }

        protected IConfigurationRoot Settings { get; set; }

        public RavenConfiguration()
        {
            
            _configBuilder = new ConfigurationBuilder();
            AddEnvironmentVariables(_configBuilder);
            AddJsonConfigurationVariables();

            Settings = _configBuilder.Build();
            Core = new CoreConfiguration();

            Replication = new ReplicationConfiguration();
            SqlReplication = new SqlReplicationConfiguration();
            Storage = new StorageConfiguration();
            Encryption = new EncryptionConfiguration();
            Indexing = new IndexingConfiguration(() => DatabaseName, () => Core.RunInMemory, () => Core.DataDirectory);
            WebSockets = new WebSocketsConfiguration();
            Monitoring = new MonitoringConfiguration();
            Queries = new QueryConfiguration();
            Patching = new PatchingConfiguration();
            DebugLog = new DebugLoggingConfiguration();
            BulkInsert = new BulkInsertConfiguration();
            Server = new ServerConfiguration();
            Memory = new MemoryConfiguration(this);
            Expiration = new ExpirationBundleConfiguration();
            Studio = new StudioConfiguration();
            Databases = new DatabaseConfiguration();
            Licensing = new LicenseConfiguration();
            Quotas = new QuotasBundleConfiguration();
            Tombstones = new TombstoneConfiguration();
        }

        private void AddJsonConfigurationVariables()
        {
            var platformPostfix = "windows";
            if (PlatformDetails.RunningOnPosix)
                platformPostfix = "posix";

            _configBuilder.AddJsonFile($"settings_{platformPostfix}.json", optional: true);
        }

        private static void AddEnvironmentVariables(IConfigurationBuilder configurationBuilder)
        {
            foreach (DictionaryEntry  de in Environment.GetEnvironmentVariables())
            {
                var s = de.Key as string;
                if (s == null)
                    continue;
                if (s.StartsWith("RAVEN_") == false)
                    continue;

                configurationBuilder.Properties[s.Replace("RAVEN_", "Raven/")] = de.Value;
            }
        }

        public DebugLoggingConfiguration DebugLog { get; set; }

        public string DatabaseName { get; set; }

        public RavenConfiguration Initialize()
        {
            Core.Initialize(Settings);
            Replication.Initialize(Settings);
            SqlReplication.Initialize(Settings);
            Queries.Initialize(Settings);
            Patching.Initialize(Settings);
            DebugLog.Initialize(Settings);
            BulkInsert.Initialize(Settings);
            Server.Initialize(Settings);
            Memory.Initialize(Settings);
            Storage.Initialize(Settings);
            Encryption.Initialize(Settings);
            Indexing.Initialize(Settings);
            Monitoring.Initialize(Settings);
            Expiration.Initialize(Settings);
            Studio.Initialize(Settings);
            Databases.Initialize(Settings);
            Licensing.Initialize(Settings);
            Quotas.Initialize(Settings);
            Tombstones.Initialize(Settings);

            PostInit();

            Initialized = true;

            return this;
        }

        public void PostInit()
        {
        }

        public void CopyParentSettings(RavenConfiguration serverConfiguration)
        {
            Encryption.UseSsl = serverConfiguration.Encryption.UseSsl;
            Encryption.UseFips = serverConfiguration.Encryption.UseFips;

            Storage.AllowOn32Bits = serverConfiguration.Storage.AllowOn32Bits;
        }

        public void SetSetting(string key, string value)
        {
            if (Initialized)
                throw new InvalidOperationException("Configuration already initialized. You cannot specify an already initialized setting.");

            Settings[key] = value;
        }

        public string GetSetting(string key)
        {
            return Settings[key];
        }

        public static string GetKey<T>(Expression<Func<RavenConfiguration, T>> getKey)
        {
            var prop = ExpressionExtensions.ToProperty(getKey);
            return prop.GetCustomAttributes<ConfigurationEntryAttribute>().OrderBy(x => x.Order).First().Key;
        }

        public static RavenConfiguration CreateFrom(RavenConfiguration parent)
        {
            var result = new RavenConfiguration
            {
                Settings = parent._configBuilder.Build()
            };

            result.Settings[GetKey(x => x.Core.RunInMemory)] = parent.Core.RunInMemory.ToString();

            return result;
        }

        public void AddCommandLine(string[] args)
        {
            _configBuilder.AddCommandLine(args);
            Settings = _configBuilder.Build();
        }
    }
}