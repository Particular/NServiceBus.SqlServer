namespace NServiceBus
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Settings;
    using Transports;
    using Transports.SQLServer;

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

        QueueAddressParser CreateAddressParser(ReadOnlySettings settings)
        {
            string defaultSchemaOverride;
            Func<string, string> schemaOverrider;

            settings.TryGet(SettingsKeys.DefaultSchemaSettingsKey, out defaultSchemaOverride);
            settings.TryGet(SettingsKeys.SchemaOverrideCallbackSettingsKey, out schemaOverrider);

            return new QueueAddressParser("dbo", defaultSchemaOverride, schemaOverrider);
        }

        bool LegacyMultiInstanceModeTurnedOn(SettingsHolder settings)
        {
            Func<string, Task<SqlConnection>> legacyModeTurnedOn;

            return settings.TryGet(SettingsKeys.LegacyMultiInstanceConnectionFactory, out legacyModeTurnedOn);
        }

        /// <summary>
        /// <see cref="TransportDefinition.Initialize" />
        /// </summary>
        protected override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
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