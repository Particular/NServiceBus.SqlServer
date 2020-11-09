namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Generic;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using System.Transactions;
    using DelayedDelivery;
    using NServiceBus.Logging;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;
    using Transport;

    /// <summary>
    /// ConfigureReceiveInfrastructure is called first, before features are started
    ///
    /// ConfigureSendInfrastructure is called last, when starting
    /// </summary>
    class SqlServerTransportInfrastructure : TransportInfrastructure
    {
        internal SqlServerTransportInfrastructure(string catalog, SettingsHolder settings, string connectionString, Func<string> localAddress, Func<LogicalAddress> logicalAddress, bool isEncrypted)
        {
            this.settings = settings;
            this.connectionString = connectionString;
            this.localAddress = localAddress;
            this.logicalAddress = logicalAddress;
            this.isEncrypted = isEncrypted;

            if (settings.HasSetting(SettingsKeys.DisableNativePubSub))
            {
                OutboundRoutingPolicy = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);
            }
            else
            {
                OutboundRoutingPolicy = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);
            }

            settings.TryGet(SettingsKeys.DefaultSchemaSettingsKey, out string defaultSchemaOverride);

            var queueSchemaSettings = settings.GetOrDefault<QueueSchemaAndCatalogSettings>();
            addressTranslator = new QueueAddressTranslator(catalog, "dbo", defaultSchemaOverride, queueSchemaSettings);
            tableBasedQueueCache = new TableBasedQueueCache(addressTranslator, !isEncrypted);
            connectionFactory = CreateConnectionFactory();

            //Configure the schema and catalog for logical endpoint-based routing
            var schemaAndCatalogSettings = settings.GetOrCreate<EndpointSchemaAndCatalogSettings>();
            settings.GetOrCreate<EndpointInstances>().AddOrReplaceInstances("SqlServer", schemaAndCatalogSettings.ToEndpointInstances());

            var pubSubSettings = settings.GetOrCreate<SubscriptionSettings>();
            var subscriptionTableName = pubSubSettings.SubscriptionTable.Qualify(defaultSchemaOverride ?? "dbo", catalog);
            // necessary evil for acceptance tests
            if (settings.TryGet<Action<string>>(SettingsKeys.SubscriptionTableQuotedQualifiedNameSetter, out var action))
            {
                action(subscriptionTableName.QuotedQualifiedName);
            }

            subscriptionStore = new PolymorphicSubscriptionStore(new SubscriptionTable(subscriptionTableName.QuotedQualifiedName, connectionFactory));
            var timeToCacheSubscriptions = pubSubSettings.TimeToCacheSubscriptions;
            if (timeToCacheSubscriptions.HasValue)
            {
                subscriptionStore = new CachedSubscriptionStore(subscriptionStore, timeToCacheSubscriptions.Value);
            }
            var subscriptionTableCreator = new SubscriptionTableCreator(subscriptionTableName, connectionFactory);
            settings.Set(subscriptionTableCreator);
        }

        SqlConnectionFactory CreateConnectionFactory()
        {
            if (settings.TryGet(SettingsKeys.ConnectionFactoryOverride, out Func<Task<SqlConnection>> factoryOverride))
            {
                return new SqlConnectionFactory(factoryOverride);
            }

            return SqlConnectionFactory.Default(connectionString);
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

        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; }

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

            Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory =
                guarantee => SelectReceiveStrategy(guarantee, scopeOptions.TransactionOptions, connectionFactory);

            var queuePurger = new QueuePurger(connectionFactory);
            var queuePeeker = new QueuePeeker(connectionFactory, queuePeekerOptions);

            IExpiredMessagesPurger expiredMessagesPurger;
            bool validateExpiredIndex;
            if (settings.GetOrDefault<bool>(SettingsKeys.PurgeEnableKey))
            {
                diagnostics.Add("NServiceBus.Transport.SqlServer.ExpiredMessagesPurger", new
                {
                    Enabled = false,
                });
                expiredMessagesPurger = new NoOpExpiredMessagesPurger();
                validateExpiredIndex = false;
            }
            else
            {
                var purgeBatchSize = settings.HasSetting(SettingsKeys.PurgeBatchSizeKey) ? settings.Get<int?>(SettingsKeys.PurgeBatchSizeKey) : null;

                diagnostics.Add("NServiceBus.Transport.SqlServer.ExpiredMessagesPurger", new
                {
                    Enabled = true,
                    BatchSize = purgeBatchSize
                });

                expiredMessagesPurger = new ExpiredMessagesPurger(_ => connectionFactory.OpenNewConnection(), purgeBatchSize);
                validateExpiredIndex = true;
            }

            var schemaVerification = new SchemaInspector(queue => connectionFactory.OpenNewConnection(), validateExpiredIndex);

            Func<string, TableBasedQueue> queueFactory = queueName => new TableBasedQueue(addressTranslator.Parse(queueName).QualifiedTableName, queueName, !isEncrypted);

            //Create delayed delivery infrastructure
            CanonicalQueueAddress delayedQueueCanonicalAddress = null;
            if (settings.GetOrDefault<bool>(SettingsKeys.DisableDelayedDelivery) == false)
            {
                if (!settings.TryGet(SettingsKeys.DelayedDeliverySuffix, out string suffix))
                {
                    suffix = "Delayed";
                }

                if (!settings.TryGet(SettingsKeys.DelayedDeliveryInterval, out TimeSpan interval))
                {
                    interval = TimeSpan.FromSeconds(1);
                }

                if (!settings.TryGet(SettingsKeys.DelayedDeliveryMatureBatchSize, out int matureBatchSize))
                {
                    matureBatchSize = 100;
                }

                settings.AddStartupDiagnosticsSection("NServiceBus.Transport.SqlServer.DelayedDelivery", new
                {
                    Native = true,
                    Suffix = suffix,
                    Interval = interval,
                    BatchSize = matureBatchSize,
                });

                delayedQueueCanonicalAddress = GetDelayedTableAddress(suffix);
                var inputQueueTable = addressTranslator.Parse(ToTransportAddress(logicalAddress())).QualifiedTableName;
                var delayedMessageTable = new DelayedMessageTable(delayedQueueCanonicalAddress.QualifiedTableName, inputQueueTable);

                //Allows dispatcher to store messages in the delayed store
                delayedMessageStore = delayedMessageTable;
                dueDelayedMessageProcessor = new DueDelayedMessageProcessor(delayedMessageTable, connectionFactory, interval, matureBatchSize);
            }

            return new TransportReceiveInfrastructure(
                () => new MessagePump(receiveStrategyFactory, queueFactory, queuePurger, expiredMessagesPurger, queuePeeker, schemaVerification, waitTimeCircuitBreaker),
                () => new QueueCreator(connectionFactory, addressTranslator, delayedQueueCanonicalAddress, createMessageBodyComputedColumn),
                () => ValidateDatabaseAccess(scopeOptions.TransactionOptions));
        }

        CanonicalQueueAddress GetDelayedTableAddress(string suffix)
        {
            var delayedQueueLogicalAddress = logicalAddress().CreateQualifiedAddress(suffix);
            var delayedQueueAddress = addressTranslator.Generate(delayedQueueLogicalAddress);
            return addressTranslator.GetCanonicalForm(delayedQueueAddress);
        }

        ReceiveStrategy SelectReceiveStrategy(TransportTransactionMode minimumConsistencyGuarantee, TransactionOptions options, SqlConnectionFactory connectionFactory)
        {
            if (minimumConsistencyGuarantee == TransportTransactionMode.TransactionScope)
            {
                return new ProcessWithTransactionScope(options, connectionFactory, new FailureInfoStorage(10000), tableBasedQueueCache);
            }

            if (minimumConsistencyGuarantee == TransportTransactionMode.SendsAtomicWithReceive)
            {
                return new ProcessWithNativeTransaction(options, connectionFactory, new FailureInfoStorage(10000), tableBasedQueueCache);
            }

            if (minimumConsistencyGuarantee == TransportTransactionMode.ReceiveOnly)
            {
                return new ProcessWithNativeTransaction(options, connectionFactory, new FailureInfoStorage(10000), tableBasedQueueCache, transactionForReceiveOnly: true);
            }

            return new ProcessWithNoTransaction(connectionFactory, tableBasedQueueCache);
        }

        IExpiredMessagesPurger CreateExpiredMessagesPurger()
        {
            if (settings.GetOrDefault<bool>(SettingsKeys.PurgeEnableKey))
            {
                diagnostics.Add("NServiceBus.Transport.SqlServer.ExpiredMessagesPurger", new
                {
                    Enabled = false,
                });
                return new NoOpExpiredMessagesPurger();
            }

            var purgeBatchSize = settings.HasSetting(SettingsKeys.PurgeBatchSizeKey) ? settings.Get<int?>(SettingsKeys.PurgeBatchSizeKey) : null;

            diagnostics.Add("NServiceBus.Transport.SqlServer.ExpiredMessagesPurger", new
            {
                Enabled = true,
                BatchSize = purgeBatchSize
            });

            return new ExpiredMessagesPurger(_ => connectionFactory.OpenNewConnection(), purgeBatchSize);
        }

        async Task<StartupCheckResult> ValidateDatabaseAccess(TransactionOptions transactionOptions)
        {
            var isDatabaseAccessible = await TryOpenDatabaseConnection().ConfigureAwait(false);

            if (!isDatabaseAccessible.Succeeded)
            {
                return isDatabaseAccessible;
            }

            return await TryEscalateToDistributedTransactions(transactionOptions).ConfigureAwait(false);
        }

        async Task<StartupCheckResult> TryOpenDatabaseConnection()
        {
            try
            {
                using (await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                }

                return StartupCheckResult.Success;
            }
            catch (Exception ex)
            {
                var message = "Could not open connection to the SQL instance. Check the original error message for details. Original error message: " + ex.Message;
                return StartupCheckResult.Failed(message);
            }
        }

        async Task<StartupCheckResult> TryEscalateToDistributedTransactions(TransactionOptions transactionOptions)
        {
            if (!settings.TryGet(out TransportTransactionMode requestedTransportTransactionMode))
            {
                requestedTransportTransactionMode = TransactionMode;
            }


            if (requestedTransportTransactionMode == TransportTransactionMode.TransactionScope)
            {
                var message = string.Empty;

                try
                {
                    using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                    using (await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                    using (await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                    {
                        scope.Complete();
                    }
                }
                catch (NotSupportedException ex)
                {
                    message = "The version of the SqlClient in use does not support enlisting SQL connections in distributed transactions. " +
                                  "Check original error message for details. " +
                                  "In case the problem is related to distributed transactions you can still use SQL Server transport but " +
                                  "should specify a different transaction mode via `EndpointConfiguration.UseTransport<SqlServerTransport>().Transactions`. " +
                                  "Note that different transaction modes may affect consistency guarantees as you can't rely on distributed " +
                                  "transactions to atomically update the database and consume a message. Original error message: " + ex.Message;
                }
                catch (SqlException sqlException)
                {
                    message = "Could not escalate to a distributed transaction while configured to use TransactionScope. Check original error message for details. " +
                                  "In case the problem is related to distributed transactions you can still use SQL Server transport but " +
                                  "should specify a different transaction mode via `EndpointConfiguration.UseTransport<SqlServerTransport>().Transactions`. " +
                                  "Note that different transaction modes may affect consistency guarantees as you can't rely on distributed " +
                                  "transactions to atomically update the database and consume a message. Original error message: " + sqlException.Message;
                }

                if (!string.IsNullOrWhiteSpace(message))
                {
                    Logger.Warn(message);
                }
            }

            return StartupCheckResult.Success;
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(
                () =>
                {
                    var multicastToUnicastConverter = new MulticastToUnicastConverter(subscriptionStore);
                    var dispatcher = new MessageDispatcher(addressTranslator, multicastToUnicastConverter, tableBasedQueueCache, delayedMessageStore, connectionFactory);
                    return dispatcher;
                },
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override Task Start()
        {
            foreach (var diagnosticSection in diagnostics)
            {
                settings.AddStartupDiagnosticsSection(diagnosticSection.Key, diagnosticSection.Value);
            }

            dueDelayedMessageProcessor?.Start();
            return Task.FromResult(0);
        }

        public override Task Stop()
        {
            return dueDelayedMessageProcessor?.Stop() ?? Task.FromResult(0);
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            return new TransportSubscriptionInfrastructure(() => new SubscriptionManager(subscriptionStore,
                settings.EndpointName(),
                localAddress()));
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
        Func<string> localAddress;
        Func<LogicalAddress> logicalAddress;
        SettingsHolder settings;
        DueDelayedMessageProcessor dueDelayedMessageProcessor;
        Dictionary<string, object> diagnostics = new Dictionary<string, object>();
        SqlConnectionFactory connectionFactory;
        ISubscriptionStore subscriptionStore;
        IDelayedMessageStore delayedMessageStore = new SendOnlyDelayedMessageStore();
        TableBasedQueueCache tableBasedQueueCache;
        bool isEncrypted;

        static ILog Logger = LogManager.GetLogger<SqlServerTransportInfrastructure>();
    }
}