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
            // required for TimeoutPersistenceVersionCheck on NSB.Core
            SettingsHolder.Set("NServiceBus.Transport.SupportsNativeTransactionSuppression", true);

            Enable<SqlServerTransport>();
            Enable<MessageDrivenSubscriptions>();
        }

        public override void Initialize()
        {
            //Until we refactor the whole address system
            CustomizeAddress();

            var connectionStringSetting = SettingsHolder.Get<string>("NServiceBus.Transport.ConnectionString");
            if (string.IsNullOrEmpty(connectionStringSetting))
            {
                throw new ArgumentException("Sql Transport connection string cannot be empty or null.");
            }

            var defaultConnectionInfo = ConnectionStringParser.AsConnectionInfo(connectionStringSetting);

            //Load all connectionstrings 
            var connectionStringCollection =
                ConfigurationManager
                .ConnectionStrings
                .Cast<ConnectionStringSettings>()
                .Where(x => x.Name.StartsWith("NServiceBus/Transport/"))
                .ToDictionary(x => x.Name.Replace("NServiceBus/Transport/", string.Empty), y =>
                {
                    var info= ConnectionStringParser.AsConnectionInfo(y.ConnectionString);
                    return info.ConnectionString;
                });

            var schemaNameCollection =
                ConfigurationManager
                .ConnectionStrings
                .Cast<ConnectionStringSettings>()
                .Where(x => x.Name.StartsWith("NServiceBus/Transport/"))
                .ToDictionary(x => x.Name.Replace("NServiceBus/Transport/", string.Empty), y =>
                {
                    var info= ConnectionStringParser.AsConnectionInfo(y.ConnectionString);
                    return info.SchemaName;
                });

            NServiceBus.Configure.Component<UnitOfWork>(DependencyLifecycle.SingleInstance)
                .ConfigureProperty( p => p.DefaultConnectionString, defaultConnectionInfo.ConnectionString );

            NServiceBus.Configure.Component<SqlServerQueueCreator>(DependencyLifecycle.InstancePerCall)
                  .ConfigureProperty(p => p.ConnectionString, defaultConnectionInfo.ConnectionString)
                  .ConfigureProperty( p => p.SchemaName, defaultConnectionInfo.SchemaName);

            NServiceBus.Configure.Component<SqlServerMessageSender>(DependencyLifecycle.InstancePerCall)
                  .ConfigureProperty(p => p.DefaultConnectionString, defaultConnectionInfo.ConnectionString)
                  .ConfigureProperty( p => p.DefaultSchemaName, defaultConnectionInfo.SchemaName)
                  .ConfigureProperty(p => p.ConnectionStringCollection, connectionStringCollection)
                  .ConfigureProperty( p => p.SchemaNameCollection, schemaNameCollection);

            NServiceBus.Configure.Component<SqlServerPollingDequeueStrategy>(DependencyLifecycle.InstancePerCall)
                  .ConfigureProperty(p => p.ConnectionString, defaultConnectionInfo.ConnectionString)
                  .ConfigureProperty(p => p.SchemaName, defaultConnectionInfo.SchemaName)
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