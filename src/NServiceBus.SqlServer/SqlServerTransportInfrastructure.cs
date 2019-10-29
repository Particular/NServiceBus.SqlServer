namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using DelayedDelivery;
    using Features;
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

            schemaAndCatalogSettings = settings.GetOrCreate<EndpointSchemaAndCatalogSettings>();
            delayedDeliverySettings = settings.GetOrCreate<DelayedDeliverySettings>();
            var timeoutManagerFeatureDisabled = !settings.IsFeatureEnabled(typeof(TimeoutManager));

            diagnostics.Add("NServiceBus.Transport.SqlServer.TimeoutManager", new
            {
                FeatureEnabled = !timeoutManagerFeatureDisabled
            });

            if (timeoutManagerFeatureDisabled)
            {
                delayedDeliverySettings.DisableTimeoutManagerCompatibility();
            }

            settings.Set(SettingsKeys.EnableMigrationMode, delayedDeliverySettings.EnableMigrationMode);

            var pubSubSettings = settings.GetOrCreate<TransportPubSubOptions>();

            subscriptions = new TableBasedSubscriptions(CreateConnectionFactory());

            if (pubSubSettings.TimeToCacheSubscription.HasValue)
            {
                subscriptions = new SubscriptionCache(subscriptions, pubSubSettings.TimeToCacheSubscription.Value);
            }
        }

        public override IEnumerable<Type> DeliveryConstraints
        {
            get
            {
                yield return typeof(DiscardIfNotReceivedBefore);
                yield return typeof(DoNotDeliverBefore);
                yield return typeof(DelayDeliveryWith);
            }
        }

        public override TransportTransactionMode TransactionMode { get; } = TransportTransactionMode.TransactionScope;

        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; } = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            if (!settings.TryGet(out SqlScopeOptions scopeOptions))
            {
                scopeOptions = new SqlScopeOptions();
            }

            settings.TryGet(out TransportTransactionMode transactionMode);
            diagnostics.Add("NServiceBus.Transport.SqlServer.Transactions", new
            {
                TransactionMode = transactionMode,
                scopeOptions.TransactionOptions.IsolationLevel,
                scopeOptions.TransactionOptions.Timeout
            });

            if (!settings.TryGet(SettingsKeys.TimeToWaitBeforeTriggering, out TimeSpan waitTimeCircuitBreaker))
            {
                waitTimeCircuitBreaker = TimeSpan.FromSeconds(30);
            }
            diagnostics.Add("NServiceBus.Transport.SqlServer.CircuitBreaker", new
            {
                TimeToWaitBeforeTriggering = waitTimeCircuitBreaker
            });

            if (!settings.TryGet(out QueuePeekerOptions queuePeekerOptions))
            {
                queuePeekerOptions = new QueuePeekerOptions();
            }

            var createMessageBodyComputedColumn = settings.GetOrDefault<bool>(SettingsKeys.CreateMessageBodyComputedColumn);

            var connectionFactory = CreateConnectionFactory();

            Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory =
                guarantee => SelectReceiveStrategy(guarantee, scopeOptions.TransactionOptions, connectionFactory);

            var queuePurger = new QueuePurger(connectionFactory);
            var queuePeeker = new QueuePeeker(connectionFactory, queuePeekerOptions);

            var expiredMessagesPurger = CreateExpiredMessagesPurger(connectionFactory);
            var schemaVerification = new SchemaInspector(queue => connectionFactory.OpenNewConnection());

            Func<string, TableBasedQueue> queueFactory = queueName => new TableBasedQueue(addressTranslator.Parse(queueName).QualifiedTableName, queueName);

            var delayedMessageStore = GetDelayedQueueTableName();

            return new TransportReceiveInfrastructure(
                () => new MessagePump(receiveStrategyFactory, queueFactory, queuePurger, expiredMessagesPurger, queuePeeker, schemaVerification, waitTimeCircuitBreaker),
                () => new QueueCreator(connectionFactory, addressTranslator, delayedMessageStore, createMessageBodyComputedColumn),
                () => CheckForAmbientTransactionEnlistmentSupport(connectionFactory, scopeOptions.TransactionOptions));
        }

        SqlConnectionFactory CreateConnectionFactory()
        {
            if (settings.TryGet(SettingsKeys.ConnectionFactoryOverride, out Func<Task<SqlConnection>> factoryOverride))
            {
                return new SqlConnectionFactory(factoryOverride);
            }

            return SqlConnectionFactory.Default(connectionString);
        }

        ReceiveStrategy SelectReceiveStrategy(TransportTransactionMode minimumConsistencyGuarantee, TransactionOptions options, SqlConnectionFactory connectionFactory)
        {
            var dispatcher = new TableBasedQueueDispatcher(connectionFactory, new TableBasedQueueOperationsReader(addressTranslator, CreateDelayedMessageTable()));

            if (minimumConsistencyGuarantee == TransportTransactionMode.TransactionScope)
            {
                return new ProcessWithTransactionScope(options, connectionFactory, new FailureInfoStorage(10000), dispatcher);
            }

            if (minimumConsistencyGuarantee == TransportTransactionMode.SendsAtomicWithReceive)
            {
                return new ProcessWithNativeTransaction(options, connectionFactory, new FailureInfoStorage(10000), dispatcher);
            }

            if (minimumConsistencyGuarantee == TransportTransactionMode.ReceiveOnly)
            {
                return new ProcessWithNativeTransaction(options, connectionFactory, new FailureInfoStorage(10000), dispatcher, transactionForReceiveOnly: true);
            }

            return new ProcessWithNoTransaction(connectionFactory, dispatcher);
        }

        ExpiredMessagesPurger CreateExpiredMessagesPurger(SqlConnectionFactory connectionFactory)
        {
            var purgeBatchSize = settings.HasSetting(SettingsKeys.PurgeBatchSizeKey) ? settings.Get<int?>(SettingsKeys.PurgeBatchSizeKey) : null;
            var enable = settings.GetOrDefault<bool>(SettingsKeys.PurgeEnableKey);

            diagnostics.Add("NServiceBus.Transport.SqlServer.ExpiredMessagesPurger", new
            {
                FeatureEnabled = enable,
                BatchSize = purgeBatchSize
            });

            return new ExpiredMessagesPurger(_ => connectionFactory.OpenNewConnection(), purgeBatchSize, enable);
        }

        async Task<StartupCheckResult> CheckForAmbientTransactionEnlistmentSupport(SqlConnectionFactory connectionFactory, TransactionOptions transactionOptions)
        {
            if (!settings.TryGet(out TransportTransactionMode requestedTransportTransactionMode))
            {
                requestedTransportTransactionMode = TransactionMode;
            }

            if (requestedTransportTransactionMode == TransportTransactionMode.TransactionScope)
            {
                try
                {
                    using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                    using (await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                    {
                        scope.Complete();
                    }
                }
                catch (NotSupportedException ex)
                {
                    var message = "The version of System.Data.SqlClient in use does not support one of the selected connection string options or " +
                                  "enlisting SQL connections in distributed transactions. Check original error message for details. " +
                                  "In case the problem is related to distributed transactions you can still use SQL Server transport but " +
                                  "specify a different transaction mode via `EndpointConfiguration.UseTransport<SqlServerTransport>().Transactions`. " +
                                  "Note that different transaction modes may affect consistency guarantees as you can't rely on distributed " +
                                  "transactions to atomically update the database and consume a message. Original error message: " + ex.Message;

                    return StartupCheckResult.Failed(message);
                }
            }

            return StartupCheckResult.Success;
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            var connectionFactory = CreateConnectionFactory();

            settings.GetOrCreate<EndpointInstances>().AddOrReplaceInstances("SqlServer", schemaAndCatalogSettings.ToEndpointInstances());

            return new TransportSendInfrastructure(
                () =>
                {
                    ITableBasedQueueOperationsReader queueOperationsReader = new TableBasedQueueOperationsReader(addressTranslator, CreateDelayedMessageTable());
                    var dispatcher = new MessageDispatcher(new TableBasedQueueDispatcher(connectionFactory, queueOperationsReader), addressTranslator, subscriptions);
                    return dispatcher;
                },
                () => Task.FromResult(StartupCheckResult.Success));
        }


        DelayedMessageTable CreateDelayedMessageTable()
        {
            var deletedQueueTableName = GetDelayedQueueTableName();

            var inputQueueTable = addressTranslator.Parse(ToTransportAddress(GetLogicalAddress())).QualifiedTableName;
            return new DelayedMessageTable(deletedQueueTableName.QualifiedTableName, inputQueueTable);
        }

        /// <summary>
        /// This method is copied from the core because there is no other way to reliable get the address of the main input queue.
        /// </summary>
        /// <returns></returns>
        LogicalAddress GetLogicalAddress()
        {
            var queueNameBase = settings.GetOrDefault<string>("BaseInputQueueName") ?? settings.EndpointName();

            //note: This is an old hack, we are passing the endpoint name to bind but we only care about the properties
            var mainInstanceProperties = BindToLocalEndpoint(new EndpointInstance(settings.EndpointName())).Properties;

            return LogicalAddress.CreateLocalAddress(queueNameBase, mainInstanceProperties);
        }

        CanonicalQueueAddress GetDelayedQueueTableName()
        {
            if (string.IsNullOrEmpty(delayedDeliverySettings.Suffix))
            {
                throw new Exception("Native delayed delivery feature requires configuring a table suffix.");
            }
            var delayedQueueLogicalAddress = GetLogicalAddress().CreateQualifiedAddress(delayedDeliverySettings.Suffix);
            var delayedQueueAddress = addressTranslator.Generate(delayedQueueLogicalAddress);
            return addressTranslator.GetCanonicalForm(delayedQueueAddress);
        }

        public override Task Start()
        {
            foreach (var diagnosticSection in diagnostics)
            {
                settings.AddStartupDiagnosticsSection(diagnosticSection.Key, diagnosticSection.Value);
            }

            settings.AddStartupDiagnosticsSection("NServiceBus.Transport.SqlServer.DelayedDelivery", new
            {
                Native = true,
                delayedDeliverySettings.Suffix,
                delayedDeliverySettings.Interval,
                BatchSize = delayedDeliverySettings.MatureBatchSize,
                TimoutManager = delayedDeliverySettings.EnableMigrationMode ? "enabled" : "disabled"
            });

            var delayedMessageTable = CreateDelayedMessageTable();
            dueDelayedMessageProcessor = new DueDelayedMessageProcessor(delayedMessageTable, CreateConnectionFactory(), delayedDeliverySettings.Interval, delayedDeliverySettings.MatureBatchSize);
            dueDelayedMessageProcessor.Start();

            return Task.FromResult(0);
        }

        public override Task Stop()
        {
            return dueDelayedMessageProcessor?.Stop() ?? Task.FromResult(0);
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            return new TransportSubscriptionInfrastructure(() => new SubscriptionManager(subscriptions, settings));
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            var schemaSettings = settings.Get<EndpointSchemaAndCatalogSettings>();

            if (schemaSettings.TryGet(instance.Endpoint, out var schema) == false)
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

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            return addressTranslator.Generate(logicalAddress).Value;
        }

        public override string MakeCanonicalForm(string transportAddress)
        {
            return addressTranslator.Parse(transportAddress).Address;
        }

        QueueAddressTranslator addressTranslator;
        string connectionString;
        SettingsHolder settings;
        EndpointSchemaAndCatalogSettings schemaAndCatalogSettings;
        DueDelayedMessageProcessor dueDelayedMessageProcessor;
        DelayedDeliverySettings delayedDeliverySettings;
        Dictionary<string, object> diagnostics = new Dictionary<string, object>();
        IManageTransportSubscriptions subscriptions;
    }
}