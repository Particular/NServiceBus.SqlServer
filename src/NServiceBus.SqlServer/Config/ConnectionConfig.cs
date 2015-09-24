namespace NServiceBus.Transports.SQLServer.Config
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data.Common;
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
            var localConnectionParams = new LocalConnectionParams(configStringSchema, connectionString, defaultSchema);
            context.Container.ConfigureComponent(() => localConnectionParams, DependencyLifecycle.SingleInstance);

            var connectionStringProvider = ConfigureConnectionStringProvider(context, localConnectionParams);
            context.Container.ConfigureComponent<IConnectionStringProvider>(() => connectionStringProvider, DependencyLifecycle.SingleInstance);
        }

        CompositeConnectionStringProvider ConfigureConnectionStringProvider(FeatureConfigurationContext context, LocalConnectionParams localConnectionParams)
        {
            var configProvidedPerEndpointConnectionStrings = CreateConfigPerEndpointConnectionStringProvider(localConnectionParams);
            var programmaticallyProvidedPerEndpointConnectionStrings = CreateProgrammaticPerEndpointConnectionStringProvider(context, localConnectionParams);

            var connectionStringProvider = new CompositeConnectionStringProvider(
                configProvidedPerEndpointConnectionStrings,
                programmaticallyProvidedPerEndpointConnectionStrings,
                new DefaultConnectionStringProvider(localConnectionParams)
                );

            return connectionStringProvider;
        }

        IConnectionStringProvider CreateConfigPerEndpointConnectionStringProvider(LocalConnectionParams localConnectionParams)
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
                        var connectionInfo = EndpointConnectionInfo.For(endpoint).UseSchema(schema);

                        var localConnectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = localConnectionParams.ConnectionString };
                        var overriddenConnectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

                        if (!localConnectionStringBuilder.EquivalentTo(overriddenConnectionStringBuilder))
                        {
                            connectionInfo = connectionInfo.UseConnectionString(connectionString);
                        }
                        return connectionInfo;
                    })
                    .ToArray();

            return new CollectionConnectionStringProvider(configConnectionStrings, localConnectionParams);
        }

        static IConnectionStringProvider CreateProgrammaticPerEndpointConnectionStringProvider(FeatureConfigurationContext context, LocalConnectionParams localConnectionParams)
        {
            var collection = context.Settings.GetOrDefault<EndpointConnectionInfo[]>(PerEndpointConnectionStringsCollectionSettingKey);
            if (collection != null)
            {
                return new CollectionConnectionStringProvider(collection, localConnectionParams);
            }
            var callback = context.Settings.GetOrDefault<Func<string, ConnectionInfo>>(PerEndpointConnectionStringsCallbackSettingKey);
            if (callback != null)
            {
                return new DelegateConnectionStringProvider(callback, localConnectionParams);
            }
            return new NullConnectionStringProvider();
        }
    }
}