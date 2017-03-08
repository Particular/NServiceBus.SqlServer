namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;
    using Transport;

    class SqlServerTransportInfrastructure : TransportInfrastructure
    {
        internal SqlServerTransportInfrastructure(QueueAddressParser addressParser, SettingsHolder settings, string connectionString)
        {
            this.addressParser = addressParser;
            this.settings = settings;
            this.connectionString = connectionString;

            endpointSchemasSettings = settings.GetOrCreate<EndpointSchemasSettings>();

            //HINT: this flag indicates that user need to explicitly turn outbox in configuration.
            RequireOutboxConsent = true;
        }

        public override IEnumerable<Type> DeliveryConstraints { get; } = new[]
        {
            typeof(DiscardIfNotReceivedBefore)
        };

        public override TransportTransactionMode TransactionMode { get; } = TransportTransactionMode.TransactionScope;

        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; } = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
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

            QueuePeekerOptions queuePeekerOptions;
            if (!settings.TryGet(out queuePeekerOptions))
            {
                queuePeekerOptions = new QueuePeekerOptions();
            }

            var connectionFactory = CreateConnectionFactory();

            Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory =
                guarantee => SelectReceiveStrategy(guarantee, scopeOptions.TransactionOptions, connectionFactory);

            var queuePurger = new QueuePurger(connectionFactory);
            var queuePeeker = new QueuePeeker(connectionFactory, queuePeekerOptions);

            var expiredMessagesPurger = CreateExpiredMessagesPurger(connectionFactory);

            Func<QueueAddress, TableBasedQueue> queueFactory = qa => new TableBasedQueue(qa);

            return new TransportReceiveInfrastructure(
                () => new MessagePump(receiveStrategyFactory, queueFactory, queuePurger, expiredMessagesPurger, queuePeeker, addressParser, waitTimeCircuitBreaker),
                () => new QueueCreator(connectionFactory, addressParser),
                () => Task.FromResult(StartupCheckResult.Success));
        }

        SqlConnectionFactory CreateConnectionFactory()
        {
            Func<Task<SqlConnection>> factoryOverride;

            if (settings.TryGet(SettingsKeys.ConnectionFactoryOverride, out factoryOverride))
            {
                return new SqlConnectionFactory(factoryOverride);
            }

            return SqlConnectionFactory.Default(connectionString);
        }

        ReceiveStrategy SelectReceiveStrategy(TransportTransactionMode minimumConsistencyGuarantee, TransactionOptions options, SqlConnectionFactory connectionFactory)
        {
            if (minimumConsistencyGuarantee == TransportTransactionMode.TransactionScope)
            {
                return new ReceiveWithTransactionScope(options, connectionFactory, new FailureInfoStorage(10000));
            }

            if (minimumConsistencyGuarantee == TransportTransactionMode.SendsAtomicWithReceive)
            {
                return new ReceiveWithNativeTransaction(options, connectionFactory, new FailureInfoStorage(10000));
            }

            if (minimumConsistencyGuarantee == TransportTransactionMode.ReceiveOnly)
            {
                return new ReceiveWithNativeTransaction(options, connectionFactory, new FailureInfoStorage(10000), transactionForReceiveOnly: true);
            }

            return new ReceiveWithNoTransaction(connectionFactory);
        }

        ExpiredMessagesPurger CreateExpiredMessagesPurger(SqlConnectionFactory connectionFactory)
        {
            var purgeTaskDelay = settings.HasSetting(SettingsKeys.PurgeTaskDelayTimeSpanKey) ? settings.Get<TimeSpan?>(SettingsKeys.PurgeTaskDelayTimeSpanKey) : null;
            var purgeBatchSize = settings.HasSetting(SettingsKeys.PurgeBatchSizeKey) ? settings.Get<int?>(SettingsKeys.PurgeBatchSizeKey) : null;

            return new ExpiredMessagesPurger(_ => connectionFactory.OpenNewConnection(), purgeTaskDelay, purgeBatchSize);
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            var connectionFactory = CreateConnectionFactory();

            settings.Get<EndpointInstances>().AddOrReplaceInstances("SqlServer", endpointSchemasSettings.ToEndpointInstances());

            return new TransportSendInfrastructure(
                () => new MessageDispatcher(new TableBasedQueueDispatcher(connectionFactory), addressParser),
                () =>
                {
                    var result = UsingV2ConfigurationChecker.Check();
                    return Task.FromResult(result);
                });
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            throw new NotImplementedException();
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            var schemaSettings = settings.Get<EndpointSchemasSettings>();

            string schema;
            if (schemaSettings.TryGet(instance.Endpoint, out schema) == false)
            {
                schema = addressParser.DefaultSchema;
            }

            return instance.SetProperty(SettingsKeys.SchemaPropertyKey, schema);
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var nonEmptyParts = new[]
            {
                logicalAddress.EndpointInstance.Endpoint,
                logicalAddress.Qualifier,
                logicalAddress.EndpointInstance.Discriminator
            }.Where(p => !string.IsNullOrEmpty(p));

            var tableName = string.Join(".", nonEmptyParts);

            string schemaName;

            logicalAddress.EndpointInstance.Properties.TryGetValue(SettingsKeys.SchemaPropertyKey, out schemaName);
            var queueAddress = new QueueAddress(tableName, schemaName);

            return queueAddress.ToString();
        }

        public override string MakeCanonicalForm(string transportAddress)
        {
            return addressParser.Parse(transportAddress).ToString();
        }

        QueueAddressParser addressParser;
        string connectionString;
        SettingsHolder settings;
        EndpointSchemasSettings endpointSchemasSettings;
    }
}