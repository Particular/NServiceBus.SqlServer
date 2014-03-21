namespace NServiceBus.Features
{
    using System;
    using System.Linq;
    using Settings;
    using Transports;
    using Transports.SQLServer;
    using System.Configuration;

    /// <summary>
    /// Configures NServiceBus to use SqlServer as the default transport
    /// </summary>
    public class SqlServerTransport : ConfigureTransport<SqlServer>
    {
        protected override string ExampleConnectionStringForErrorMessage
        {
            get { return @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True"; }
        }

        protected override void InternalConfigure(Configure config)
        {
            
            Enable<SqlServerTransport>();
            Enable<MessageDrivenSubscriptions>();
        }

        public override void Initialize()
        {
            //Until we refactor the whole address system
            CustomizeAddress();
            
            var defaultConnectionString = SettingsHolder.Get<string>("NServiceBus.Transport.ConnectionString");

            //Load all connectionstrings 
            var collection =
                ConfigurationManager
                .ConnectionStrings
                .Cast<ConnectionStringSettings>()
                .Where(x => x.Name.StartsWith("NServiceBus/Transport/"))
                .ToDictionary(x => x.Name.Replace("NServiceBus/Transport/", String.Empty), y => y.ConnectionString);

            if (String.IsNullOrEmpty(defaultConnectionString))
            {
                throw new ArgumentException("Sql Transport connection string cannot be empty or null.");
            }

            NServiceBus.Configure.Component<UnitOfWork>(DependencyLifecycle.SingleInstance);

            NServiceBus.Configure.Component<SqlServerQueueCreator>(DependencyLifecycle.InstancePerCall)
                  .ConfigureProperty(p => p.ConnectionString, defaultConnectionString);

            NServiceBus.Configure.Component<SqlServerMessageSender>(DependencyLifecycle.InstancePerCall)
                  .ConfigureProperty(p => p.DefaultConnectionString, defaultConnectionString)
                  .ConfigureProperty(p => p.ConnectionStringCollection, collection);

            NServiceBus.Configure.Component<SqlServerPollingDequeueStrategy>(DependencyLifecycle.InstancePerCall)
                  .ConfigureProperty(p => p.ConnectionString, defaultConnectionString)
                  .ConfigureProperty(p => p.PurgeOnStartup, ConfigurePurging.PurgeRequested);
        }

        static void CustomizeAddress()
        {
            Address.IgnoreMachineName();

            if (!SettingsHolder.GetOrDefault<bool>("ScaleOut.UseSingleBrokerQueue"))
            {
                Address.InitializeLocalAddress(Address.Local.Queue + "." + Address.Local.Machine);
            }
         
        }
    }
}