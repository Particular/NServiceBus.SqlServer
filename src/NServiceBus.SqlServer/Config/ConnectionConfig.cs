namespace NServiceBus.Transports.SQLServer.Config
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using NServiceBus.Features;

    class ConnectionConfig : ConfigBase
    {
        public const string DefaultSchemaSettingsKey = "SqlServer.SchemaName";
        public const string PerEndpointConnectionStringsCallbackSettingKey = "SqlServer.PerEndpointConnectrionStringsCallback";
        public const string PerEndpointConnectionStringsCollectionSettingKey = "SqlServer.PerEndpointConnectionStringsCollection";

        readonly List<ConnectionStringSettings> connectionStrings;

        public ConnectionConfig(List<ConnectionStringSettings> connectionStrings)
        {
            this.connectionStrings = connectionStrings;
        }

        public override void Configure(FeatureConfigurationContext context, string connectionStringWithSchema)
        {
            var defaultSchema = context.Settings.GetOrDefault<string>(DefaultSchemaSettingsKey);
            string configStringSchema;
            var connectionString = connectionStringWithSchema.ExtractSchemaName(out configStringSchema);
            var localConnectionParams = new ConnectionParams(null, configStringSchema, connectionString, defaultSchema);
            context.Container.ConfigureComponent(() => localConnectionParams, DependencyLifecycle.SingleInstance);

            var connectionStringProvider = ConfigureConnectionStringProvider(context, localConnectionParams);
            context.Container.ConfigureComponent<IConnectionStringProvider>(() => connectionStringProvider, DependencyLifecycle.SingleInstance);
        }

        CompositeConnectionStringProvider ConfigureConnectionStringProvider(FeatureConfigurationContext context, ConnectionParams defaultConnectionParams)
        {
            const string transportConnectionStringPrefix = "NServiceBus/Transport/";
            var configConnectionStrings =
                connectionStrings
                    .Where(x => x.Name.StartsWith(transportConnectionStringPrefix))
                    .Select(x =>
                    {
                        string schema;
                        var connectionString = x.ConnectionString.ExtractSchemaName(out schema);
                        var endpoint = x.Name.Replace(transportConnectionStringPrefix, String.Empty);
                        return EndpointConnectionInfo.For(endpoint).UseConnectionString(connectionString).UseSchema(schema);
                    });

            var configProvidedPerEndpointConnectionStrings = new CollectionConnectionStringProvider(configConnectionStrings, defaultConnectionParams);
            var programmaticallyProvidedPerEndpointConnectionStrings = CreateProgrammaticPerEndpointConnectionStringProvider(context, defaultConnectionParams);

            var connectionStringProvider = new CompositeConnectionStringProvider(
                configProvidedPerEndpointConnectionStrings,
                programmaticallyProvidedPerEndpointConnectionStrings,
                new DefaultConnectionStringProvider(defaultConnectionParams)
                );
            return connectionStringProvider;
        }

        static IConnectionStringProvider CreateProgrammaticPerEndpointConnectionStringProvider(FeatureConfigurationContext context, ConnectionParams defaultConnectionParams)
        {
            var collection = context.Settings.GetOrDefault<IEnumerable<EndpointConnectionInfo>>(PerEndpointConnectionStringsCollectionSettingKey);
            if (collection != null)
            {
                return new CollectionConnectionStringProvider(collection, defaultConnectionParams);
            }
            var callback = context.Settings.GetOrDefault<Func<string, ConnectionInfo>>(PerEndpointConnectionStringsCallbackSettingKey);
            if (callback != null)
            {
                return new DelegateConnectionStringProvider(callback, defaultConnectionParams);
            }
            return new NullConnectionStringProvider();
        }
    }
}