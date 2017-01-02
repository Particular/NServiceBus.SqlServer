namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using Logging;
    using Routing;
    using Settings;

    class AutoDetectQueueLocation
    {
        public const string EnableKey = "NServiceBus.SqlServer.AutoDetectQueueLocation";
        public const string SchemaFilterKey = "NServiceBus.SqlServer.AutoDetectQueueLocation.Schemas";
        public const string CatalogFilterKey = "NServiceBus.SqlServer.AutoDetectQueueLocation.Catalogs";

        static ILog log = LogManager.GetLogger<AutoDetectQueueLocation>();

        string[] catalogs;
        string[] schemas;
        SqlConnectionFactory connectionFactory;
        EndpointInstances endpointInstances;
        TimeSpan updateInterval;
        AsyncTimer timer;

        public static AutoDetectQueueLocation TryConfigure(ReadOnlySettings settings, SqlConnectionFactory connectionFactory)
        {
            if (!settings.HasExplicitValue(EnableKey))
            {
                return null;
            }
            var schemas = settings.GetOrDefault<string[]>(SchemaFilterKey);
            var catalogs = settings.GetOrDefault<string[]>(CatalogFilterKey);

            var detector = new AutoDetectQueueLocation(catalogs, schemas, connectionFactory, settings.Get<EndpointInstances>(), TimeSpan.FromMinutes(1));
            return detector;
        }

        public AutoDetectQueueLocation(string[] catalogs, string[] schemas, SqlConnectionFactory connectionFactory, EndpointInstances endpointInstances, TimeSpan updateInterval)
        {
            this.catalogs = catalogs;
            this.schemas = schemas;
            this.connectionFactory = connectionFactory;
            this.endpointInstances = endpointInstances;
            this.updateInterval = updateInterval;
        }

        public Task Start()
        {
            timer = new AsyncTimer();
            timer.Start(() => UpdateInstanceTable(false), updateInterval, exception => log.Error("Error while trying to update queue location information", exception));
            return UpdateInstanceTable(true);
        }

        public Task Stop()
        {
            return timer.Stop();
        }

        public async Task UpdateInstanceTable(bool throwOnDuplicates)
        {
            var instances = await Detect().ConfigureAwait(false);
            if (DetectDuplicates(instances, throwOnDuplicates))
            {
                endpointInstances.AddOrReplaceInstances("AutoDetectQueueLocation", instances);
            }
        }

        static bool DetectDuplicates(IList<EndpointInstance> instances, bool throwOnDuplicates)
        {
            var instanceGroups = instances.GroupBy(i => i.Endpoint);
            var duplicates = instanceGroups.Where(g => g.Count() > 1).ToArray();
            if (duplicates.Length > 0)
            {
                var textParts = duplicates.Select(x => $"{x.Key} ({FormatLocations(x)})");
                var duplicateText = string.Join(", ", textParts);
                if (throwOnDuplicates)
                {
                    throw new Exception($"Duplicate location detected for {duplicateText}.");
                }
                log.Error($"Duplicate location detected for {duplicateText}. Skipping updating of routing tables.");
                return false;
            }
            return true;
        }

        static string FormatLocations(IEnumerable<EndpointInstance> duplicates)
        {
            return string.Join(",", duplicates.Select(FormatLocation));
        }

        static string FormatLocation(EndpointInstance endpointInstance)
        {
            var schema = endpointInstance.Properties[SettingsKeys.SchemaPropertyKey];
            var catalog = endpointInstance.Properties[SettingsKeys.CatalogPropertyKey];
            return $"[{catalog}].[{schema}]";
        }

        public async Task<IList<EndpointInstance>> Detect()
        {
            var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false);

            var catalogsToScan = catalogs ?? await ListAllCatalogs(connection).ConfigureAwait(false);

            var results = new List<EndpointInstance>();

            foreach (var catalog in catalogsToScan)
            {
                results.AddRange(await Detect(catalog, connection).ConfigureAwait(false));
            }

            return results;
        }

        async Task<IList<EndpointInstance>> Detect(string catalog, SqlConnection connection)
        {
            var results = new List<EndpointInstance>();
            using (var command = new SqlCommand($@"
SELECT DISTINCT t.TABLE_SCHEMA, t.TABLE_NAME
FROM [{catalog}].[INFORMATION_SCHEMA].[TABLES] t
INNER JOIN [{catalog}].[INFORMATION_SCHEMA].[COLUMNS] cId ON t.TABLE_NAME = cId.TABLE_NAME AND cId.COLUMN_NAME = 'Id' AND cId.DATA_TYPE = 'uniqueidentifier'
INNER JOIN [{catalog}].[INFORMATION_SCHEMA].[COLUMNS] cCorrelationId ON t.TABLE_NAME = cCorrelationId.TABLE_NAME AND cCorrelationId.COLUMN_NAME = 'CorrelationId' AND cCorrelationId.DATA_TYPE = 'varchar'
INNER JOIN [{catalog}].[INFORMATION_SCHEMA].[COLUMNS] cReplyToAddress ON t.TABLE_NAME = cReplyToAddress.TABLE_NAME AND cReplyToAddress.COLUMN_NAME = 'ReplyToAddress' AND cReplyToAddress.DATA_TYPE = 'varchar'
INNER JOIN [{catalog}].[INFORMATION_SCHEMA].[COLUMNS] cRecoverable ON t.TABLE_NAME = cRecoverable.TABLE_NAME AND cRecoverable.COLUMN_NAME = 'Recoverable' AND cRecoverable.DATA_TYPE = 'bit'
INNER JOIN [{catalog}].[INFORMATION_SCHEMA].[COLUMNS] cExpires ON t.TABLE_NAME = cExpires.TABLE_NAME AND cExpires.COLUMN_NAME = 'Expires' AND cExpires.DATA_TYPE = 'datetime'
INNER JOIN [{catalog}].[INFORMATION_SCHEMA].[COLUMNS] cHeaders ON t.TABLE_NAME = cHeaders.TABLE_NAME AND cHeaders.COLUMN_NAME = 'Headers' AND cHeaders.DATA_TYPE = 'varchar'
INNER JOIN [{catalog}].[INFORMATION_SCHEMA].[COLUMNS] cBody ON t.TABLE_NAME = cBody.TABLE_NAME AND cBody.COLUMN_NAME = 'Body' AND cBody.DATA_TYPE = 'varbinary'
INNER JOIN [{catalog}].[INFORMATION_SCHEMA].[COLUMNS] cRowVersion ON t.TABLE_NAME = cRowVersion.TABLE_NAME AND cRowVersion.COLUMN_NAME = 'RowVersion' AND cRowVersion.DATA_TYPE = 'bigint'
WHERE t.TABLE_TYPE = 'BASE TABLE'", connection))
            {
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var schema = reader.GetString(0);
                        var table = reader.GetString(1);
                        if (schemas != null && schemas.All(x => !string.Equals(x, schema, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            continue;
                        }
                        var properties = new Dictionary<string, string>
                        {
                            [SettingsKeys.SchemaPropertyKey] = schema,
                            [SettingsKeys.CatalogPropertyKey] = catalog
                        };
                        var instance = new EndpointInstance(table, null, properties);
                        results.Add(instance);
                    }
                }
            }
            return results;
        }

        static async Task<IList<string>> ListAllCatalogs(SqlConnection connection)
        {
            var results = new List<string>();
            using (var command = new SqlCommand("SELECT [name] FROM [master].[sys].[databases] WHERE database_id > 4", connection))
            {
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var name = reader.GetString(0);
                        results.Add(name);
                    }
                }
            }
            return results;
        }
    }
}
