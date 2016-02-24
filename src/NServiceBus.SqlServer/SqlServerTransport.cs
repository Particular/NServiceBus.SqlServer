namespace NServiceBus
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Transports.SQLServer;
    using NServiceBus.Transports.SQLServer.Legacy.MultiInstance;

    /// <summary>
    /// SqlServer Transport
    /// </summary>
    public class SqlServerTransport : TransportDefinition
    {
        QueueAddressParser CreateAddressParser(ReadOnlySettings settings)
        {
            string defaultSchemaOverride;
            Func<string, string> schemaOverrider;

            settings.TryGet(SettingsKeys.DefaultSchemaSettingsKey, out defaultSchemaOverride);
            settings.TryGet(SettingsKeys.SchemaOverrideCallbackSettingsKey, out schemaOverrider);

            var parser = new QueueAddressParser("dbo", defaultSchemaOverride, schemaOverrider);

            return parser;
        }

        bool LegacyMultiInstanceModeTurnedOn(SettingsHolder settings)
        {
            Func<string, Task<SqlConnection>> legacyModeTurnedOn;

            return settings.TryGet(SettingsKeys.LegacyMultiInstanceConnectionFactory, out legacyModeTurnedOn);
        }

        /// <summary>
        /// <see cref="TransportDefinition.Initialize"/>
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        protected override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            var addressParser = CreateAddressParser(settings);

            if (LegacyMultiInstanceModeTurnedOn(settings))
            {
                return new LegacySqlServerTransportInfrastructure(addressParser, settings, connectionString);
            }

            return new SqlServerTransportInfrastructure(addressParser, settings, connectionString);
        }

        /// <summary>
        /// <see cref="TransportDefinition.ExampleConnectionStringForErrorMessage"/>
        /// </summary>
        public override string ExampleConnectionStringForErrorMessage => @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";

        /// <summary>
        /// <see cref="TransportDefinition.RequiresConnectionString"/>
        /// </summary>
        public override bool RequiresConnectionString => true;
    }
}