using System;
using System.Collections.Generic;
using Raven.Abstractions.Data;
using Raven.Abstractions.Logging;
using Raven.Client.Data;
using Raven.Server.Documents.Expiration;
using Raven.Server.Documents.PeriodicExport;
using Raven.Server.Documents.Versioning;
using Raven.Server.ServerWide.Context;
using Raven.Server.Utils;
using Sparrow.Json.Parsing;
using Sparrow.Logging;

namespace Raven.Server.Documents
{
    public class BundleLoader : IDisposable
    {
        private readonly Logger _logger;

        private readonly DocumentDatabase _database;
        public VersioningStorage VersioningStorage;
        public ExpiredDocumentsCleaner ExpiredDocumentsCleaner;
        public PeriodicExportRunner PeriodicExportRunner;

        public BundleLoader(DocumentDatabase database)
        {
            _database = database;
            _database.Notifications.OnSystemDocumentChange += HandleSystemDocumentChange;
            _logger = LoggingSource.Instance.GetLogger<BundleLoader>(_database.Name);
            InitializeBundles();
        }

        /// <summary>
        /// Configure the database bundles if no changes has accord or when server start
        /// </summary>
        public void InitializeBundles()
        {
            DocumentsOperationContext context;
            using (_database.DocumentsStorage.ContextPool.AllocateOperationContext(out context))
            {
                context.OpenReadTransaction();
                var versioningConfiguration = _database.DocumentsStorage.Get(context,
                    Constants.Versioning.RavenVersioningConfiguration);
                if (versioningConfiguration != null)
                {
                    VersioningStorage = VersioningStorage.LoadConfigurations(_database);
                    if (_logger.IsInfoEnabled)
                        _logger.Info("Versioning configuration enabled");
                }

                var expirationConfiguration = _database.DocumentsStorage.Get(context,
                    Constants.Expiration.ConfigurationDocumentKey);
                if (expirationConfiguration != null)
                {
                    ExpiredDocumentsCleaner?.Dispose();
                    ExpiredDocumentsCleaner = ExpiredDocumentsCleaner.LoadConfigurations(_database);
                    if (_logger.IsInfoEnabled)
                        _logger.Info("Expiration configuration enabled");
                }

                var periodicExportConfiguration = _database.DocumentsStorage.Get(context,
                    Constants.PeriodicExport.ConfigurationDocumentKey);
                if (periodicExportConfiguration != null)
                {
                    PeriodicExportRunner?.Dispose();
                    PeriodicExportRunner = PeriodicExportRunner.LoadConfigurations(_database);
                    if (_logger.IsInfoEnabled)
                        _logger.Info("PeriodicExport configuration enabled");
                }
            }
        }

        public void HandleSystemDocumentChange(DocumentChangeNotification notification)
        {
            var key = notification.Key;
            if (key.Equals(Constants.Versioning.RavenVersioningConfiguration, StringComparison.OrdinalIgnoreCase))
            {
                VersioningStorage = VersioningStorage.LoadConfigurations(_database);

                if (_logger.IsInfoEnabled)
                    _logger.Info($"Versioning configuration was {(VersioningStorage != null ? "disabled" : "enabled")}");
            }
            else if (key.Equals(Constants.Expiration.ConfigurationDocumentKey, StringComparison.OrdinalIgnoreCase))
            {
                ExpiredDocumentsCleaner?.Dispose();
                ExpiredDocumentsCleaner = ExpiredDocumentsCleaner.LoadConfigurations(_database);

                if (_logger.IsInfoEnabled)
                    _logger.Info($"Expiration configuration was {(ExpiredDocumentsCleaner != null ? "enabled" : "disabled")}");
            }
            else if (key.Equals(Constants.PeriodicExport.ConfigurationDocumentKey, StringComparison.OrdinalIgnoreCase))
            {
                PeriodicExportRunner?.Dispose();
                PeriodicExportRunner = PeriodicExportRunner.LoadConfigurations(_database);

                if (_logger.IsInfoEnabled)
                    _logger.Info($"PeriodicExport configuration was {(PeriodicExportRunner != null ? "enabled" : "disabled")}");
            }
        }

        public List<string> GetActiveBundles()
        {
            var res = new List<string>();
            if (ExpiredDocumentsCleaner != null)
                res.Add(BundleTypeToName[ExpiredDocumentsCleaner.GetType()]);
            if (VersioningStorage != null)
                res.Add(BundleTypeToName[VersioningStorage.GetType()]);
            if (PeriodicExportRunner != null)
                res.Add(BundleTypeToName[PeriodicExportRunner.GetType()]);
            return res;
        }

        private static readonly Dictionary<Type, string> BundleTypeToName = new Dictionary<Type, string>
        {
            {typeof(VersioningStorage), "Versioning"},
            {typeof(ExpiredDocumentsCleaner), "Expiration"},
            {typeof(PeriodicExportRunner), "PeriodicExport"}
        };

        public void Dispose()
        {
            _database.Notifications.OnSystemDocumentChange -= HandleSystemDocumentChange;

            var exceptionAggregator = new ExceptionAggregator(_logger, $"Could not dispose {nameof(BundleLoader)}");
            exceptionAggregator.Execute(() =>
            {
                ExpiredDocumentsCleaner?.Dispose();
                ExpiredDocumentsCleaner = null;
            });
            exceptionAggregator.Execute(() =>
            {
                PeriodicExportRunner?.Dispose();
                PeriodicExportRunner = null;
            });
            exceptionAggregator.ThrowIfNeeded();
        }

        public DynamicJsonValue GetBackupInfo()
        {
            if (PeriodicExportRunner == null)
            {
                return null;
            }

            return new DynamicJsonValue
            {
                [nameof(BackupInfo.IncrementalBackupInterval)] = PeriodicExportRunner.IncrementalInterval,
                [nameof(BackupInfo.FullBackupInterval)] = PeriodicExportRunner.FullExportInterval,
                [nameof(BackupInfo.LastIncrementalBackup)] = PeriodicExportRunner.ExportTime,
                [nameof(BackupInfo.LastFullBackup)] = PeriodicExportRunner.FullExportTime
            };
        }
    }
}