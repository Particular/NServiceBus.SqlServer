namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Routing;
    using Settings;
    using Transport;

    class LegacySqlServerTransportInfrastructure : SqlServerTransportInfrastructure
    {
        public LegacySqlServerTransportInfrastructure(QueueAddressParser addressParser, SettingsHolder settings, string connectionString)
            : base(addressParser, settings, connectionString)
        {
            this.addressParser = addressParser;
            this.settings = settings;

            this.endpointSchemasSettings = settings.GetOrCreate<EndpointSchemasSettings>();
        }

        LegacySqlConnectionFactory CreateLegacyConnectionFactory()
        {
            var factory = settings.Get<Func<string, Task<SqlConnection>>>(SettingsKeys.LegacyMultiInstanceConnectionFactory);

            return new LegacySqlConnectionFactory(factory);
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            QueuePeekerOptions peekerOptions;
            if (!settings.TryGet(out peekerOptions))
            {
                peekerOptions = new QueuePeekerOptions();
            }

            var connectionFactory = CreateLegacyConnectionFactory();

            var queuePurger = new LegacyQueuePurger(connectionFactory);
            var queuePeeker = new LegacyQueuePeeker(connectionFactory, peekerOptions);

            var expiredMessagesPurger = CreateExpiredMessagesPurger(connectionFactory);

            SqlScopeOptions scopeOptions;
            if (!settings.TryGet(out scopeOptions))
            {
                scopeOptions = new SqlScopeOptions();
            }

            TimeSpan waitTimeCircuitBreaker;
            if (!settings.TryGet(SettingsKeys.TimeToWaitBeforeTriggering, out waitTimeCircuitBreaker))
            {
                waitTimeCircuitBreaker = TimeSpan.FromSeconds(30);
            }

            Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory =
                guarantee =>
                {
                    if (guarantee != TransportTransactionMode.TransactionScope)
                    {
                        throw new Exception("Legacy multiinstance mode is supported only with TransportTransactionMode=TransactionScope");
                    }

                    return new LegacyReceiveWithTransactionScope(scopeOptions.TransactionOptions, connectionFactory, new FailureInfoStorage(1000));
                };

            Func<QueueAddress, TableBasedQueue> queueFactory = qa => new TableBasedQueue(qa);

            return new TransportReceiveInfrastructure(
                () => new MessagePump(receiveStrategyFactory, queueFactory, queuePurger, expiredMessagesPurger, queuePeeker, addressParser, waitTimeCircuitBreaker),
                () => new LegacyQueueCreator(connectionFactory, addressParser),
                () => Task.FromResult(StartupCheckResult.Success));
        }

        ExpiredMessagesPurger CreateExpiredMessagesPurger(LegacySqlConnectionFactory connectionFactory)
        {
            var purgeTaskDelay = settings.HasSetting(SettingsKeys.PurgeTaskDelayTimeSpanKey) ? settings.Get<TimeSpan?>(SettingsKeys.PurgeTaskDelayTimeSpanKey) : null;
            var purgeBatchSize = settings.HasSetting(SettingsKeys.PurgeBatchSizeKey) ? settings.Get<int?>(SettingsKeys.PurgeBatchSizeKey) : null;

            return new ExpiredMessagesPurger(queue => connectionFactory.OpenNewConnection(queue.TransportAddress), purgeTaskDelay, purgeBatchSize);
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            var connectionFactory = CreateLegacyConnectionFactory();

            settings.Get<EndpointInstances>().AddOrReplaceInstances("SqlServer", endpointSchemasSettings.ToEndpointInstances());

            return new TransportSendInfrastructure(
                () => new MessageDispatcher(new LegacyTableBasedQueueDispatcher(connectionFactory, addressParser), addressParser),
                () =>
                {
                    var result = UsingV2ConfigurationChecker.Check();
                    return Task.FromResult(result);
                });
        }

        QueueAddressParser addressParser;
        SettingsHolder settings;
        EndpointSchemasSettings endpointSchemasSettings;
    }
}