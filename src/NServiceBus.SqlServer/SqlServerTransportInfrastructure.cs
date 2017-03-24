namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;
    using Transport;

    class SqlServerTransportInfrastructure : TransportInfrastructure
    {
        internal SqlServerTransportInfrastructure(QueueAddressTranslator addressTranslator, SettingsHolder settings, string connectionString)
        {
            this.addressTranslator = addressTranslator;
            this.settings = settings;
            this.connectionString = connectionString;

            this.schemaAndCatalogSettings = settings.GetOrCreate<EndpointSchemaAndCatalogSettings>();
            settings.GetOrCreate<ForwardingConfiguration>();

            //HINT: this flag indicates that user need to explicitly turn outbox in configuration.
            RequireOutboxConsent = true;
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.DeliveryConstraints" />
        /// </summary>
        public override IEnumerable<Type> DeliveryConstraints { get; } = new[]
        {
            typeof(DiscardIfNotReceivedBefore)
        };

        /// <summary>
        /// <see cref="TransportInfrastructure.TransactionMode" />
        /// </summary>
        public override TransportTransactionMode TransactionMode { get; } = TransportTransactionMode.TransactionScope;

        /// <summary>
        /// <see cref="TransportInfrastructure.OutboundRoutingPolicy" />
        /// </summary>
        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; } = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);

        /// <summary>
        /// <see cref="TransportInfrastructure.ConfigureReceiveInfrastructure" />
        /// </summary>
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

            Func<string, TableBasedQueue> queueFactory = queueName => new TableBasedQueue(addressTranslator.Parse(queueName).QualifiedTableName, queueName);

            return new TransportReceiveInfrastructure(
                () => new MessagePump(receiveStrategyFactory, queueFactory, queuePurger, expiredMessagesPurger, queuePeeker, waitTimeCircuitBreaker),
                () => new QueueCreator(connectionFactory, addressTranslator),
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

        static ReceiveStrategy SelectReceiveStrategy(TransportTransactionMode minimumConsistencyGuarantee, TransactionOptions options, SqlConnectionFactory connectionFactory)
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

        /// <summary>
        /// <see cref="TransportInfrastructure.ConfigureSendInfrastructure" />
        /// </summary>
        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            var connectionFactory = CreateConnectionFactory();
            var forwardingSettings = settings.GetOrCreate<ForwardingConfiguration>();
            settings.Get<EndpointInstances>().AddOrReplaceInstances("SqlServer", schemaAndCatalogSettings.ToEndpointInstances());

            return new TransportSendInfrastructure(
                () => new MessageDispatcher(new TableBasedQueueDispatcher(connectionFactory, addressTranslator, forwardingSettings), addressTranslator),
                () =>
                {
                    var result = UsingV2ConfigurationChecker.Check();
                    return Task.FromResult(result);
                });
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.ConfigureSubscriptionInfrastructure" />
        /// </summary>
        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.BindToLocalEndpoint" />
        /// </summary>
        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            var schemaSettings = settings.Get<EndpointSchemaAndCatalogSettings>();

            string schema;
            if (schemaSettings.TryGet(instance.Endpoint, out schema) == false)
            {
                schema = addressTranslator.DefaultSchema;
            }
            var result = instance.SetProperty(SettingsKeys.SchemaPropertyKey, schema);
            if (addressTranslator.DefaultCatalog != null)
            {
                result = result.SetProperty(SettingsKeys.CatalogPropertyKey, addressTranslator.DefaultCatalog);
            }
            return result;
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.ToTransportAddress" />
        /// </summary>
        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            return addressTranslator.Generate(logicalAddress).Value;
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.MakeCanonicalForm" />
        /// </summary>
        public override string MakeCanonicalForm(string transportAddress)
        {
            return addressTranslator.Parse(transportAddress).Address;
        }

        QueueAddressTranslator addressTranslator;
        string connectionString;
        SettingsHolder settings;
        EndpointSchemaAndCatalogSettings schemaAndCatalogSettings;
    }
}