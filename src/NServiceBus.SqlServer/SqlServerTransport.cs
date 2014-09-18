namespace NServiceBus.Features
{
    using System;
    using System.Linq;
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
        protected override string ExampleConnectionStringForErrorMessage
        {
            get { return @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True"; }
        }

        protected override string GetLocalAddress(ReadOnlySettings settings)
        {
            if (!settings.GetOrDefault<bool>("ScaleOut.UseSingleBrokerQueue"))
            {
                return settings.EndpointName() + "." + RuntimeEnvironment.MachineName;
            }

            return null;
        }

        protected override void Configure(FeatureConfigurationContext context, string connectionString)
        {
            //Until we refactor the whole address system
            Address.IgnoreMachineName();

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
                .ConfigureProperty(p => p.ConnectionString, connectionString);

            container.ConfigureComponent<SqlServerMessageSender>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.DefaultConnectionString, connectionString)
                .ConfigureProperty(p => p.ConnectionStringCollection, collection);

            container.ConfigureComponent<SqlServerPollingDequeueStrategy>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.ConnectionString, connectionString);

            context.Container.ConfigureComponent(b => new SqlServerStorageContext(b.Build<PipelineExecutor>(), connectionString), DependencyLifecycle.InstancePerUnitOfWork);
        }
    }
}