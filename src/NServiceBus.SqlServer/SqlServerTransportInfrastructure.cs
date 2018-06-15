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
            delayedDeliverySettings = settings.GetOrDefault<DelayedDeliverySettings>();
            var timeoutManagerFeatureDisabled = settings.GetOrDefault<FeatureState>(typeof(TimeoutManager).FullName) == FeatureState.Disabled;
            settings.AddStartupDiagnosticsSection("NServiceBus.Transport.SqlServer.TimeoutManager", new
            {
                FeatureEnabled = !timeoutManagerFeatureDisabled
            });
            if (delayedDeliverySettings != null && timeoutManagerFeatureDisabled)
            {
                delayedDeliverySettings.DisableTimeoutManagerCompatibility();
            }
        }

        public override IEnumerable<Type> DeliveryConstraints
        {
            get
            {
                yield return typeof(DiscardIfNotReceivedBefore);
                if (delayedDeliverySettings != null && delayedDeliverySettings.TimeoutManagerDisabled)
                {
                    yield return typeof(DoNotDeliverBefore);
                    yield return typeof(DelayDeliveryWith);
                }
            }
        }

        public override TransportTransactionMode TransactionMode { get; } = TransportTransactionMode.TransactionScope;

        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; } = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            if (!settings.TryGet(out SqlScopeOptions scopeOptions))
            {
                scopeOptions = new SqlScopeOptions();
            }

            settings.TryGet(out TransportTransactionMode transactionMode);
            settings.AddStartupDiagnosticsSection("NServiceBus.Transport.SqlServer.Transactions", new
            {
                TransactionMode = transactionMode,
                scopeOptions.TransactionOptions.IsolationLevel,
                scopeOptions.TransactionOptions.Timeout
            });

            if (!settings.TryGet(SettingsKeys.TimeToWaitBeforeTriggering, out TimeSpan waitTimeCircuitBreaker))
            {
                waitTimeCircuitBreaker = TimeSpan.FromSeconds(30);
            }
            settings.AddStartupDiagnosticsSection("NServiceBus.Transport.SqlServer.CircuitBreaker", new
            {
                TimeToWaitBeforeTriggering = waitTimeCircuitBreaker
            });

            if (!settings.TryGet(out QueuePeekerOptions queuePeekerOptions))
            {
                queuePeekerOptions = new QueuePeekerOptions();
            }

            var connectionFactory = CreateConnectionFactory();

            Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory =
                guarantee => SelectReceiveStrategy(guarantee, scopeOptions.TransactionOptions, connectionFactory);

            var queuePurger = new QueuePurger(connectionFactory);
            var queuePeeker = new QueuePeeker(connectionFactory, queuePeekerOptions);

            var expiredMessagesPurger = CreateExpiredMessagesPurger(connectionFactory);
            var schemaVerification = new SchemaInspector(queue => connectionFactory.OpenNewConnection());

            Func<string, TableBasedQueue> queueFactory = queueName => new TableBasedQueue(addressTranslator.Parse(queueName).QualifiedTableName, queueName);

            var delayedMessageStore = GetDelayedQueueTableName();

            var sendInfra = ConfigureSendInfrastructure();
            return new TransportReceiveInfrastructure(
                () =>
                {
                    var pump = new MessagePump(receiveStrategyFactory, queueFactory, queuePurger, expiredMessagesPurger, queuePeeker, schemaVerification, waitTimeCircuitBreaker);
                    if (delayedDeliverySettings == null)
                    {
                        return pump;
                    }
                    var dispatcher = sendInfra.DispatcherFactory();
                    var delayedMessageProcessor = new DelayedMessageProcessor(dispatcher);
                    return new DelayedDeliveryMessagePump(pump, delayedMessageProcessor);
                },
                () =>
                {
                    var creator = new QueueCreator(connectionFactory, addressTranslator);
                    if (delayedDeliverySettings == null)
                    {
                        return creator;
                    }
                    return new DelayedDeliveryQueueCreator(connectionFactory, creator, delayedMessageStore);
                },
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

        static ReceiveStrategy SelectReceiveStrategy(TransportTransactionMode minimumConsistencyGuarantee, TransactionOptions options, SqlConnectionFactory connectionFactory)
        {
            if (minimumConsistencyGuarantee == TransportTransactionMode.TransactionScope)
            {
                return new ProcessWithTransactionScope(options, connectionFactory, new FailureInfoStorage(10000));
            }

            if (minimumConsistencyGuarantee == TransportTransactionMode.SendsAtomicWithReceive)
            {
                return new ProcessWithNativeTransaction(options, connectionFactory, new FailureInfoStorage(10000));
            }

            if (minimumConsistencyGuarantee == TransportTransactionMode.ReceiveOnly)
            {
                return new ProcessWithNativeTransaction(options, connectionFactory, new FailureInfoStorage(10000), transactionForReceiveOnly: true);
            }

            return new ProcessWithNoTransaction(connectionFactory);
        }

        ExpiredMessagesPurger CreateExpiredMessagesPurger(SqlConnectionFactory connectionFactory)
        {
            var purgeBatchSize = settings.HasSetting(SettingsKeys.PurgeBatchSizeKey) ? settings.Get<int?>(SettingsKeys.PurgeBatchSizeKey) : null;
            var enable = settings.GetOrDefault<bool>(SettingsKeys.PurgeEnableKey);

            settings.AddStartupDiagnosticsSection("NServiceBus.Transport.SqlServer.ExpiredMessagesPurger", new
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
                catch (NotSupportedException)
                {
                    var message = "The version of System.Data.SqlClient in use does not support enlisting SQL connections in ambient transactions, so the TransactionScope transport transaction mode cannot be used. Use `EndpointConfiguration.UseTransport<SqlServerTransport>().Transactions` to select a different transport transaction mode.";
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
                    ITableBasedQueueOperationsReader queueOperationsReader = new TableBasedQueueOperationsReader(addressTranslator);
                    if (delayedDeliverySettings != null)
                    {
                        queueOperationsReader = new DelayedDeliveryTableBasedQueueOperationsReader(CreateDelayedMessageTable(), queueOperationsReader);
                    }
                    var dispatcher = new MessageDispatcher(new TableBasedQueueDispatcher(connectionFactory, queueOperationsReader), addressTranslator);
                    return dispatcher;
                },
                () =>
                {
                    var result = StartupCheckResult.Success;
                    if (delayedDeliverySettings != null)
                    {
                        result = DelayedDeliveryInfrastructure.CheckForInvalidSettings(settings);
                    }
                    return Task.FromResult(result);
                });
        }

        DelayedMessageTable CreateDelayedMessageTable()
        {
            var delatedQueueTableName = GetDelayedQueueTableName();

            var inputQueueTable = addressTranslator.Parse(ToTransportAddress(GetLogicalAddress())).QualifiedTableName;
            return new DelayedMessageTable(delatedQueueTableName.QualifiedTableName, inputQueueTable);
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
            if (delayedDeliverySettings == null)
            {
                return null;
            }
            if (string.IsNullOrEmpty(delayedDeliverySettings.Suffix))
            {
                throw new Exception("Native delayed delivery feature requires configuring a table suffix.");
            }
            var delayedQueueLogialAddress = GetLogicalAddress().CreateQualifiedAddress(delayedDeliverySettings.Suffix);
            var delayedQueueAddress = addressTranslator.Generate(delayedQueueLogialAddress);
            return addressTranslator.GetCanonicalForm(delayedQueueAddress);
        }

        public override Task Start()
        {
            if (delayedDeliverySettings == null)
            {
                settings.AddStartupDiagnosticsSection("NServiceBus.Transport.SqlServer.DelayedDelivery", new
                {
                    Native = false
                });
                return Task.FromResult(0);
            }

            settings.AddStartupDiagnosticsSection("NServiceBus.Transport.SqlServer.DelayedDelivery", new
            {
                Native = true,
                delayedDeliverySettings.Suffix,
                delayedDeliverySettings.Interval,
                BatchSize = delayedDeliverySettings.MatureBatchSize,
                TimoutManager = delayedDeliverySettings.TimeoutManagerDisabled ? "disabled" : "enabled"
            });
            var delayedMessageTable = CreateDelayedMessageTable();
            delayedMessageHandler = new DelayedMessageHandler(delayedMessageTable, CreateConnectionFactory(), delayedDeliverySettings.Interval, delayedDeliverySettings.MatureBatchSize);
            delayedMessageHandler.Start();
            return Task.FromResult(0);
        }

        public override Task Stop()
        {
            return delayedMessageHandler?.Stop() ?? Task.FromResult(0);
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            throw new NotImplementedException();
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
        DelayedMessageHandler delayedMessageHandler;
        DelayedDeliverySettings delayedDeliverySettings;
    }
}