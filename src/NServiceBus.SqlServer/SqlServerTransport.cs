namespace NServiceBus.Features
{
    using System;
    using Settings;
    using Transports;
    using Transports.SQLServer;
    using NServiceBus.Transports.SQLServer.DatabaseAccess;

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
            
            IDatabaseAccessInfo dbAccess;
            var useStoredProcedures = SettingsHolder.Get<bool>("NServiceBus.Transport.UseStoredProcedures");
            if (useStoredProcedures)
                dbAccess = new StoredProceduresAccessInfo();
            else
                dbAccess = new InlineSqlAccesInfo();

            var connectionString = SettingsHolder.Get<string>("NServiceBus.Transport.ConnectionString");
            if (String.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Sql Transport connection string cannot be empty or null.");
            }

            NServiceBus.Configure.Component<UnitOfWork>(DependencyLifecycle.SingleInstance);

            //TODO: What to do with this?
            NServiceBus.Configure.Component<SqlServerQueueCreator>(DependencyLifecycle.InstancePerCall);

            NServiceBus.Configure.Component<SqlServerMessageSender>(DependencyLifecycle.InstancePerCall)
                  .ConfigureProperty(p => p.ConnectionString, connectionString)
                  .ConfigureProperty(p => p.DatabaseAccessInfo, dbAccess);

            NServiceBus.Configure.Component<SqlServerPollingDequeueStrategy>(DependencyLifecycle.InstancePerCall)
                  .ConfigureProperty(p => p.ConnectionString, connectionString)
                  .ConfigureProperty(p => p.PurgeOnStartup, ConfigurePurging.PurgeRequested)
                  .ConfigureProperty(p => p.DatabaseAccessInfo, dbAccess);
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