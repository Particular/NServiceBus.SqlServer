namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Transactions;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transports.SQLServer.Config;
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
            new PurgingConfig()
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

        protected override Func<IBuilder, ReceiveBehavior> GetReceiveBehaviorFactory(ReceiveOptions receiveOptions)
        {
            if (receiveOptions.Transactions.IsTransactional)
            {
                var transactionOptions = new TransactionOptions
                {
                    IsolationLevel = receiveOptions.Transactions.IsolationLevel,
                    Timeout = receiveOptions.Transactions.TransactionTimeout
                };

                if (receiveOptions.Transactions.SuppressDistributedTransactions)
                {
                    return builder =>
                    {
                        var connectionInfo = builder.Build<LocalConnectionParams>();
                        var errorQueue = new TableBasedQueue(receiveOptions.ErrorQueue, connectionInfo.Schema);
                        return new NativeTransactionReceiveBehavior(connectionInfo.ConnectionString, errorQueue, transactionOptions);
                    };
                }
                return builder =>
                {
                    var connectionInfo = builder.Build<LocalConnectionParams>();
                    var errorQueue = new TableBasedQueue(receiveOptions.ErrorQueue, connectionInfo.Schema);
                    return new AmbientTransactionReceiveBehavior(connectionInfo.ConnectionString, errorQueue, transactionOptions);
                };
            }

            return builder =>
            {
                var connectionInfo = builder.Build<LocalConnectionParams>();
                var errorQueue = new TableBasedQueue(receiveOptions.ErrorQueue, connectionInfo.Schema);
                return new NoTransactionReceiveBehavior(connectionInfo.ConnectionString, errorQueue);
            };
        }

        protected override string GetLocalAddress(ReadOnlySettings settings)
        {
            return settings.EndpointName();
        }

        protected override void Configure(FeatureConfigurationContext context, string connectionStringWithSchema)
        {
            if (String.IsNullOrEmpty(connectionStringWithSchema))
            {
                throw new ArgumentException("Sql Transport connection string cannot be empty or null.");
            }

            foreach (var config in configs)
            {
                config.Configure(context, connectionStringWithSchema);
            }

            context.Container.ConfigureComponent<SqlServerMessageSender>(DependencyLifecycle.InstancePerCall);
            context.Container.ConfigureComponent<SqlServerQueueCreator>(DependencyLifecycle.InstancePerCall);
            context.Container.ConfigureComponent<SqlServerPollingDequeueStrategy>(DependencyLifecycle.InstancePerCall);
            context.Container.ConfigureComponent<SqlServerStorageContext>(DependencyLifecycle.InstancePerUnitOfWork);
        }
    }

}