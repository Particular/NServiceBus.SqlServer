namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
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

            this.schemaAndCatalogSettings = settings.GetOrCreate<SchemaAndCatalogSettings>();

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

        /// <summary>
        /// <see cref="TransportInfrastructure.ConfigureSendInfrastructure" />
        /// </summary>
        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            var connectionFactory = CreateConnectionFactory();

            settings.Get<EndpointInstances>().AddOrReplaceInstances("SqlServer", schemaAndCatalogSettings.ToEndpointInstances());

            return new TransportSendInfrastructure(
                () => new MessageDispatcher(new TableBasedQueueDispatcher(connectionFactory), addressParser),
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
            var schemaSettings = settings.Get<SchemaAndCatalogSettings>();

            string schema;
            if (schemaSettings.TryGet(instance.Endpoint, out schema) == false)
            {
                schema = addressParser.DefaultSchema;
            }

            var result = instance.SetProperty(SettingsKeys.SchemaPropertyKey, schema);

            if (!settings.HasSetting(SettingsKeys.ConnectionFactoryOverride) && !settings.HasSetting(SettingsKeys.LegacyMultiInstanceConnectionFactory))
            {
                var parser = new DbConnectionStringBuilder
                {
                    ConnectionString = connectionString
                };

                object catalog;
                if (parser.TryGetValue("Initial Catalog", out catalog) || parser.TryGetValue("Database", out catalog))
                {
                    result = result.SetProperty(SettingsKeys.CatalogPropertyKey, (string) catalog);
                }
                else
                {
                    throw new Exception("Initial Catalog property is mandatory in the connection string.");
                }
            }
            else if (!settings.HasSetting("PublicReturnAddress"))
            {
                throw new Exception("When using a custom connection factory or legacy multi-instance mode it is required to also provide a custom return address using EndpointConfiguration.OverridePublicReturnAddress.");
            }
            return result;
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

            string schemaName, catalogName;

            logicalAddress.EndpointInstance.Properties.TryGetValue(SettingsKeys.SchemaPropertyKey, out schemaName);
            logicalAddress.EndpointInstance.Properties.TryGetValue(SettingsKeys.CatalogPropertyKey, out catalogName);
            var queueAddress = new QueueAddress(catalogName, schemaName ?? "", tableName);

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
        SchemaAndCatalogSettings schemaAndCatalogSettings;
    }
}