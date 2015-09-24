namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using NServiceBus.Transports.SQLServer.Config;
    using Pipeline;
    using Settings;
    using Transports;
    using Transports.SQLServer;

    
    class SqlServerTransportFeature : ConfigureTransport
    {
        readonly List<ConfigBase> configs = new List<ConfigBase>()
        {
            new CallbackConfig(),
            new CircuitBreakerConfig(),
            new ConnectionConfig(ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().ToList()),
            new PurgingConfig(),
            new SqlConnectionFactoryConfig()
        };

        public SqlServerTransportFeature()
        {
            Defaults(s =>
            {
                foreach (var config in configs)
                {
                    config.SetUpDefaults(s);
                }
            });
        }

        protected override string ExampleConnectionStringForErrorMessage
        {
            get { return @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True"; }
        }

        protected override string GetLocalAddress(ReadOnlySettings settings)
        {
            return settings.EndpointName();
        }

        protected override void Configure(FeatureConfigurationContext context, string connectionStringWithSchema)
        {
            //Until we refactor the whole address system
            Address.IgnoreMachineName();

            if (String.IsNullOrEmpty(connectionStringWithSchema))
            {
                throw new ArgumentException("Sql Transport connection string cannot be empty or null.");
            }

            foreach (var config in configs)
            {
                config.Configure(context, connectionStringWithSchema);
            }

            context.Container.ConfigureComponent(
                b => new SqlServerMessageSender(
                    b.Build<IConnectionStringProvider>(),
                    new ContextualConnectionStore(b.Build<PipelineExecutor>()),
                    new ContextualCallbackAddressStore(b.Build<PipelineExecutor>().CurrentContext),
                    b.Build<ConnectionFactory>()),
                DependencyLifecycle.InstancePerCall);

            if (!context.Settings.GetOrDefault<bool>("Endpoint.SendOnly"))
            {
                context.Container.ConfigureComponent<TransportNotifications>(DependencyLifecycle.SingleInstance);
                context.Container.ConfigureComponent<SqlServerQueueCreator>(DependencyLifecycle.InstancePerCall);

                var errorQueue = ErrorQueueSettings.GetConfiguredErrorQueue(context.Settings);
                context.Container.ConfigureComponent(b => new ReceiveStrategyFactory(new ContextualConnectionStore(b.Build<PipelineExecutor>()), b.Build<LocalConnectionParams>(), errorQueue, b.Build<ConnectionFactory>()), DependencyLifecycle.InstancePerCall);

                context.Container.ConfigureComponent<SqlServerPollingDequeueStrategy>(DependencyLifecycle.InstancePerCall);
                context.Container.ConfigureComponent(b => new SqlServerStorageContext(b.Build<PipelineExecutor>(), b.Build<LocalConnectionParams>()), DependencyLifecycle.InstancePerUnitOfWork);
            }
        }
    }

}