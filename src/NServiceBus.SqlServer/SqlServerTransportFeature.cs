namespace NServiceBus.Features
{
    using System;
    using System.Linq;
    using NServiceBus.ObjectBuilder;
    using Pipeline;
    using Settings;
    using Support;
    using Transports;
    using Transports.SQLServer;
    using System.Configuration;

    class SqlServerTransportFeature : ConfigureTransport
    {
        public const string UseCallbackReceiverSettingKey = "SqlServer.UseCallbackReceiver";
        public const string MaxConcurrencyForCallbackReceiverSettingKey = "SqlServer.MaxConcurrencyForCallbackReceiver";
        public const string PerEndpointConnectrionStringsSettingKey = "SqlServer.PerEndpointConnectrionStrings";

        public SqlServerTransportFeature()
        {
            Defaults(s =>
            {
                s.SetDefault(UseCallbackReceiverSettingKey, true);
                s.SetDefault(MaxConcurrencyForCallbackReceiverSettingKey, 1);
                s.SetDefault(PerEndpointConnectrionStringsSettingKey, new NullConnectionStringProvider());
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

        protected override void Configure(FeatureConfigurationContext context, string connectionString)
        {
            //Until we refactor the whole address system
            Address.IgnoreMachineName();

            var useCallbackReceiver = context.Settings.Get<bool>(UseCallbackReceiverSettingKey);
            var maxConcurrencyForCallbackReceiver = context.Settings.Get<int>(MaxConcurrencyForCallbackReceiverSettingKey);

            var queueName = GetLocalAddress(context.Settings);
            var callbackQueue = string.Format("{0}.{1}", queueName, RuntimeEnvironment.MachineName);
            var errorQueue = ErrorQueueSettings.GetConfiguredErrorQueue(context.Settings);

            var connectionStringProvider = ConfigureConnectionStringProvider(context, connectionString);

            var container = context.Container;

            container.ConfigureComponent<SqlServerQueueCreator>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.ConnectionString, connectionString);

            var senderConfig = container.ConfigureComponent<SqlServerMessageSender>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.DefaultConnectionString, connectionString)
                .ConfigureProperty(p => p.ConnectionStringProvider, connectionStringProvider);

            container.ConfigureComponent<ReceiveStrategyFactory>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.ErrorQueue, errorQueue)
                .ConfigureProperty(p => p.ConnectionString, connectionString);

            container.ConfigureComponent<SqlServerPollingDequeueStrategy>(DependencyLifecycle.InstancePerCall);

            ConfigurePurging(context.Settings, container, connectionString);

            context.Container.ConfigureComponent(b => new SqlServerStorageContext(b.Build<PipelineExecutor>(), connectionString), DependencyLifecycle.InstancePerUnitOfWork);

            if (useCallbackReceiver)
            {
                senderConfig.ConfigureProperty(p => p.CallbackQueue, callbackQueue);

                var callbackAddress = Address.Parse(callbackQueue);

                context.Container.ConfigureComponent<CallbackQueueCreator>(DependencyLifecycle.InstancePerCall)
                    .ConfigureProperty(p => p.Enabled, true)
                    .ConfigureProperty(p => p.CallbackQueueAddress, callbackAddress);

                context.Pipeline.Register<ReadCallbackAddressBehavior.Registration>();
            }
            context.Container.RegisterSingleton(new SecondaryReceiveConfiguration(workQueue =>
            {
                //if this isn't the main queue we shouldn't use callback receiver
                if (!useCallbackReceiver || workQueue != queueName)
                {
                    return SecondaryReceiveSettings.Disabled();
                }

                return SecondaryReceiveSettings.Enabled(callbackQueue, maxConcurrencyForCallbackReceiver);
            }));
        }

        static CompositeConnectionStringProvider ConfigureConnectionStringProvider(FeatureConfigurationContext context, string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Sql Transport connection string cannot be empty or null.");
            }

            const string transportConnectionStringPrefix = "NServiceBus/Transport/";
            var configConnectionStrings =
                ConfigurationManager
                    .ConnectionStrings
                    .Cast<ConnectionStringSettings>()
                    .Where(x => x.Name.StartsWith(transportConnectionStringPrefix))
                    .Select(x => new EndpointConnectionString(x.Name.Replace(transportConnectionStringPrefix, String.Empty), x.ConnectionString));

            var configProvidedPerEndpointConnectionStrings = new CollectionConnectionStringProvider(configConnectionStrings);
            var programmaticallyProvidedPerEndpointConnectionStrings = context.Settings.Get<IConnectionStringProvider>(PerEndpointConnectrionStringsSettingKey);

            var connectionStringProvider = new CompositeConnectionStringProvider(
                programmaticallyProvidedPerEndpointConnectionStrings,
                configProvidedPerEndpointConnectionStrings,
                new DefaultConnectionStringProvider(connectionString)
                );
            return connectionStringProvider;
        }

        static void ConfigurePurging(ReadOnlySettings settings, IConfigureComponents container, string connectionString)
        {
            bool purgeOnStartup;
            if (settings.TryGet("Transport.PurgeOnStartup", out purgeOnStartup) && purgeOnStartup)
            {
                container.ConfigureComponent<QueuePurger>(DependencyLifecycle.SingleInstance)
                    .ConfigureProperty(p => p.ConnectionString, connectionString);
            }
            else
            {
                container.ConfigureComponent<NullQueuePurger>(DependencyLifecycle.SingleInstance);
            }
        }
    }
}