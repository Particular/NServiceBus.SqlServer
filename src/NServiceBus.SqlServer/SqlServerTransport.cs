namespace NServiceBus
{
    using System;
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

        QueueAddressParser CreateAddressParser(ReadOnlySettings settings)
        {
            string defaultSchemaOverride;
            settings.TryGet(SettingsKeys.DefaultSchemaSettingsKey, out defaultSchemaOverride);

            var queueSchemaSettings = settings.GetOrDefault<TableSchemasSettings>();

            return new QueueAddressParser("dbo", defaultSchemaOverride, queueSchemaSettings);
        }

        bool LegacyMultiInstanceModeTurnedOn(SettingsHolder settings)
        {
            Func<string, Task<SqlConnection>> legacyModeTurnedOn;

            return settings.TryGet(SettingsKeys.LegacyMultiInstanceConnectionFactory, out legacyModeTurnedOn);
        }

        /// <summary>
        /// <see cref="TransportDefinition.Initialize" />
        /// </summary>
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            var addressParser = CreateAddressParser(settings);

            if (LegacyMultiInstanceModeTurnedOn(settings))
            {
                return new LegacySqlServerTransportInfrastructure(addressParser, settings, connectionString);
            }

            return new SqlServerTransportInfrastructure(addressParser, settings, connectionString);
        }
    }
}