namespace NServiceBus
{
    using System;
    using System.Data.Common;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Routing;
    using Settings;
    using Transport;
    using Transport.SQLServer;

    /// <summary>
    /// SqlServer Transport
    /// </summary>
    public class SqlServerTransport : TransportDefinition
    {
        /// <summary>
        /// <see cref="TransportDefinition.ExampleConnectionStringForErrorMessage" />
        /// </summary>
        public override string ExampleConnectionStringForErrorMessage => @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";

        /// <summary>
        /// <see cref="TransportDefinition.RequiresConnectionString" />
        /// </summary>
        public override bool RequiresConnectionString => false;

        static bool LegacyMultiInstanceModeTurnedOn(SettingsHolder settings)
        {
            var legacyMode = settings.TryGet(SettingsKeys.LegacyMultiInstanceConnectionFactory, out Func<string, Task<SqlConnection>> _);
            if (legacyMode && settings.HasSetting(SettingsKeys.MultiCatalogEnabled))
            {
                throw new Exception("Multi-catalog configuration is not supported in legacy multi instance mode. Please configure each catalog using a separate connection string.");
            }
            return legacyMode;
        }

        /// <summary>
        /// <see cref="TransportDefinition.Initialize" />
        /// </summary>
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            settings.TryGet(SettingsKeys.DefaultSchemaSettingsKey, out string defaultSchemaOverride);
            var queueSchemaSettings = settings.GetOrDefault<QueueSchemaAndCatalogSettings>();

            var catalog = GetDefaultCatalog(settings, connectionString);
            var addressParser = new QueueAddressTranslator(catalog, "dbo", defaultSchemaOverride, queueSchemaSettings);
            return new SqlServerTransportInfrastructure(addressParser, settings, connectionString);
        }

        static string GetDefaultCatalog(SettingsHolder settings, string connectionString)
        {
            if (settings.TryGet(SettingsKeys.ConnectionFactoryOverride, out Func<Task<SqlConnection>> factoryOverride))
            {
                using (var connection = factoryOverride().GetAwaiter().GetResult())
                {
                    connectionString = connection.ConnectionString;
                }
            }
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("Either connection string or connection factory has to be specified in the SQL Server transport configuration.");
            }
            var parser = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };
            if (parser.TryGetValue("Initial Catalog", out var catalog) ||
                parser.TryGetValue("database", out catalog))
            {
                return (string)catalog;
            }
            throw new Exception("Initial Catalog property is mandatory in the connection string.");
        }
    }
}