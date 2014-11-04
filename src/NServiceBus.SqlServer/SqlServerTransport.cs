namespace NServiceBus.Features
{
    using System;
    using System.Linq;
    using NServiceBus.Logging;
    using Pipeline;
    using Settings;
    using Support;
    using Transports;
    using Transports.SQLServer;
    using System.Configuration;

    /// <summary>
    /// Configures NServiceBus to use SqlServer as the default transport
    /// </summary>
    class SqlServerTransport : ConfigureTransport
    {
        public const string UseCallbackReceiverSettingKey = "SqlServer.UseCallbackReceiver";
        public const string MaxConcurrencyForCallbackReceiverSettingKey = "SqlServer.MaxConcurrencyForCallbackReceiver";
        public const string SchemaName = "SqlServer.SchemaName";
        public const string SchemaAwareAddressing = "SqlServer.SchemaAwareAddressing";

        public SqlServerTransport()
        {
            Defaults(s =>
            {
                s.SetDefault(UseCallbackReceiverSettingKey, true);
                s.SetDefault(MaxConcurrencyForCallbackReceiverSettingKey, 1);
                s.SetDefault(SchemaName, null);
                s.SetDefault(SchemaAwareAddressing, null);
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
            var schemaName = context.Settings.GetOrDefault<string>(SchemaName);
            var schemaAwareAddressing = context.Settings.GetOrDefault<bool?>(SchemaAwareAddressing);
            if (!schemaAwareAddressing.HasValue && schemaName != null)
            {
                Logger.Warn("Endpoint uses non-default schema but schema-aware addressing has not been enabled (disabled by default). " 
                    + "This endpoint WILL NOT be able to send messages to endpoints that use non-default schema. "
                    + "If this is a desired behavior, disable schama-aware addressing by calling EnableSchemaAwareAddressing(false).");
            }

            var queueName = GetLocalAddress(context.Settings);
            var callbackQueue = string.Format("{0}.{1}", queueName, RuntimeEnvironment.MachineName);

            //Load all connectionstrings 
            var collection =
                ConfigurationManager
                    .ConnectionStrings
                    .Cast<ConnectionStringSettings>()
                    .Where(x => x.Name.StartsWith("NServiceBus/Transport/"))
                    .ToDictionary(x => x.Name.Replace("NServiceBus/Transport/", String.Empty), y => y.ConnectionString);

            if (String.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Sql Transport connection string cannot be empty or null.");
            }

            var container = context.Container;

            container.ConfigureComponent<SqlServerQueueCreator>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.ConnectionString, connectionString)
                .ConfigureProperty(p => p.SchemaName, schemaName);

            container.ConfigureComponent<SqlServerMessageSender>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.DefaultConnectionString, connectionString)
                .ConfigureProperty(p => p.ConnectionStringCollection, collection)
                .ConfigureProperty(p => p.CallbackQueue, callbackQueue)
                .ConfigureProperty(p => p.SchemaAwareAddressing, schemaAwareAddressing.HasValue && schemaAwareAddressing.Value);


            container.ConfigureComponent<SqlServerPollingDequeueStrategy>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.ConnectionString, connectionString)
                .ConfigureProperty(p => p.SchemaName, schemaName);

            context.Container.ConfigureComponent(b => new SqlServerStorageContext(b.Build<PipelineExecutor>(), connectionString), DependencyLifecycle.InstancePerUnitOfWork);

            if (useCallbackReceiver)
            {
                var callbackAddress = Address.Parse(callbackQueue);

                context.Container.ConfigureComponent<CallbackQueueCreator>(DependencyLifecycle.InstancePerCall)
                    .ConfigureProperty(p => p.Enabled, true)
                    .ConfigureProperty(p => p.CallbackQueueAddress, callbackAddress);

                context.Pipeline.Register<PromoteCallbackQueueBehavior.Registration>();
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

        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerTransport));
    }
}