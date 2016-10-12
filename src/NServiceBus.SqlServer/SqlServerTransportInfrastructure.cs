namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using DelayedDelivery;
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

            this.endpointSchemasSettings = settings.GetOrCreate<EndpointSchemasSettings>();

            //HINT: this flag indicates that user need to explicitly turn outbox in configusration.
            RequireOutboxConsent = true;
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.DeliveryConstraints" />
        /// </summary>
        public override IEnumerable<Type> DeliveryConstraints
        {
            get
            {
                yield return typeof(DiscardIfNotReceivedBefore);
                if (settings.HasSetting<DelayedDeliverySettings>())
                {
                    yield return typeof(DoNotDeliverBefore);
                    yield return typeof(DelayDeliveryWith);
                }
            }
        }

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

            Func<QueueAddress, TableBasedQueue> queueFactory = qa => new TableBasedQueue(qa);

            QueueAddress delayedMessageStore;
            var nativeDelayedDeliverySettings = settings.GetOrDefault<DelayedDeliverySettings>();
            if (nativeDelayedDeliverySettings != null)
            {
                var localAddress = addressParser.Parse(settings.LocalAddress());
                var delayedTableName = localAddress.TableName + "." + nativeDelayedDeliverySettings.TableSuffix;
                delayedMessageStore = new QueueAddress(delayedTableName, localAddress.SchemaName);
            }
            else
            {
                delayedMessageStore = null;
            }

            return new TransportReceiveInfrastructure(
                () => new MessagePump(receiveStrategyFactory, queueFactory, queuePurger, expiredMessagesPurger, queuePeeker, addressParser, waitTimeCircuitBreaker),
                () => new QueueCreator(connectionFactory, addressParser, delayedMessageStore),
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

        /// <summary>
        /// <see cref="TransportInfrastructure.ConfigureSendInfrastructure" />
        /// </summary>
        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            var connectionFactory = CreateConnectionFactory();

            settings.Get<EndpointInstances>().AddOrReplaceInstances("SqlServer", endpointSchemasSettings.ToEndpointInstances());
            var nativeDelayedDeliverySettings = settings.GetOrDefault<DelayedDeliverySettings>();
            DelayedMessageTable delayedMessageTable = null;
            if (nativeDelayedDeliverySettings != null)
            {
                delayedMessageTable = new DelayedMessageTable(settings.LocalAddress(), nativeDelayedDeliverySettings.TableSuffix, addressParser);
            }
            return new TransportSendInfrastructure(
                () => new MessageDispatcher(new TableBasedQueueDispatcher(connectionFactory, delayedMessageTable,  addressParser), addressParser),
                () =>
                {
                    var result = UsingV2ConfigurationChecker.Check();
                    return Task.FromResult(result);
                });
        }

        public override Task Start()
        {
            var nativeDelayedDeliverySettings = settings.GetOrDefault<DelayedDeliverySettings>();
            if (nativeDelayedDeliverySettings != null)
            {
                var delayedMessageTable = new DelayedMessageTable(settings.LocalAddress(), nativeDelayedDeliverySettings.TableSuffix, addressParser);
                delayedMessageHandler = new MaturedDelayMessageHandler(delayedMessageTable, CreateConnectionFactory(), TimeSpan.FromSeconds(nativeDelayedDeliverySettings.ProcessingResolution));
                delayedMessageHandler.Start();
            }
            return Task.FromResult(0);
        }

        public override Task Stop()
        {
            if (delayedMessageHandler != null)
            {
                return delayedMessageHandler.Stop();
            }
            return Task.FromResult(0);
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
            var schemaSettings = settings.Get<EndpointSchemasSettings>();

            string schema;
            if (schemaSettings.TryGet(instance.Endpoint, out schema) == false)
            {
                schema = addressParser.DefaultSchema;
            }

            return instance.SetProperty(SettingsKeys.SchemaPropertyKey, schema);
        }

        /// <summary>
        /// <see cref="TransportInfrastructure.ToTransportAddress" />
        /// </summary>
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

        /// <summary>
        /// <see cref="TransportInfrastructure.MakeCanonicalForm" />
        /// </summary>
        public override string MakeCanonicalForm(string transportAddress)
        {
            return addressParser.Parse(transportAddress).ToString();
        }
        
        QueueAddressParser addressParser;
        string connectionString;
        SettingsHolder settings;
        EndpointSchemasSettings endpointSchemasSettings;
        MaturedDelayMessageHandler delayedMessageHandler;
    }
}