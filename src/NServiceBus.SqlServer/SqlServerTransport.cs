namespace NServiceBus
{
    using System;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Transports.SQLServer;

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

        }

        EndpointConnectionStringLookup GetEndpointConnectionStringLookup(ReadOnlySettings settings, string defaultConnectionString)
        {
            Func<string, Task<string>> endpointConnectionLookup;

            if (settings.TryGet(SettingsKeys.EndpointConnectionLookupFunc, out endpointConnectionLookup))
            {
                return new EndpointConnectionStringLookup(endpointConnectionLookup);
            }

            return EndpointConnectionStringLookup.Default(defaultConnectionString);
        /// <summary>
        /// <see cref="TransportDefinition.Initialize"/>
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        protected override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
            var endpointConnectionStringLookup = GetEndpointConnectionStringLookup(context.Settings, context.ConnectionString);
        {
            var addressParser = CreateAddressParser(settings);

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