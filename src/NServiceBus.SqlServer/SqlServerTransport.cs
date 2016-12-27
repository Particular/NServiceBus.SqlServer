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
    public class SqlServerTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
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
            Func<string, Task<SqlConnection>> legacyModeTurnedOn;

            return settings.TryGet(SettingsKeys.LegacyMultiInstanceConnectionFactory, out legacyModeTurnedOn);
        }

        /// <summary>
        /// <see cref="TransportDefinition.Initialize" />
        /// </summary>
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            string defaultSchemaOverride;
            settings.TryGet(SettingsKeys.DefaultSchemaSettingsKey, out defaultSchemaOverride);
            var queueSchemaSettings = settings.GetOrDefault<QueueSchemaAndCatalogSettings>();

            if (LegacyMultiInstanceModeTurnedOn(settings))
            {
                var addressParser = new LegacyQueueAddressTranslator("dbo", defaultSchemaOverride, queueSchemaSettings);

                return new LegacySqlServerTransportInfrastructure(addressParser, settings);
            }
            else
            {
                var catalog = GetDefaultCatalog(settings, connectionString);
                var addressParser = new QueueAddressTranslator((string)catalog, "dbo", defaultSchemaOverride, queueSchemaSettings);
                return new SqlServerTransportInfrastructure(addressParser, settings, connectionString);
            }
        }

        static object GetDefaultCatalog(SettingsHolder settings, string connectionString)
        {
            Func<Task<SqlConnection>> factoryOverride;
            if (settings.TryGet(SettingsKeys.ConnectionFactoryOverride, out factoryOverride))
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
            object catalog;
            if (!parser.TryGetValue("Initial Catalog", out catalog) && !parser.TryGetValue("database", out catalog))
            {
                throw new Exception("Initial Catalog property is mandatory in the connection string.");
            }
            return catalog;
        }
    }
}