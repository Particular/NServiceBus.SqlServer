namespace NServiceBus.Transports.SQLServer.Legacy.MultiInstance
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using NServiceBus.Settings;

    class LegacySqlServerTransportInfrastructure : SqlServerTransportInfrastructure
    {
        QueueAddressParser addressParser;
        SettingsHolder settings;

        public LegacySqlServerTransportInfrastructure(QueueAddressParser addressParser, SettingsHolder settings, string connectionString) 
            : base(addressParser, settings, connectionString)
        {
            this.addressParser = addressParser;
            this.settings = settings;
        }

        LegacySqlConnectionFactory CreateLegacyConnectionFactory()
        {
            var factory = settings.Get<Func<string, Task<SqlConnection>>>(SettingsKeys.LegacyMultiInstanceConnectionFactory);

            return new LegacySqlConnectionFactory(factory);
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            var connectionFactory = CreateLegacyConnectionFactory();

            var queuePurger = new LegacyQueuePurger(connectionFactory);
            var queuePeeker = new LegacyQueuePeeker(connectionFactory);

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

                    return new LegacyReceiveWithTransactionScope(scopeOptions.TransactionOptions, connectionFactory);
                };

            return new TransportReceiveInfrastructure(
                () => new MessagePump(receiveStrategyFactory, queuePurger, expiredMessagesPurger, queuePeeker, addressParser, waitTimeCircuitBreaker),
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

            return new TransportSendInfrastructure(
                () => new LegacyMessageDispatcher(connectionFactory, addressParser),
                () =>
                {
                    var result = UsingV2ConfigurationChecker.Check();
                    return Task.FromResult(result);
                });
        }
    }
}