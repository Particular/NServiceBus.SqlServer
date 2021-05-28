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
    using Logging;
    using Transport;
    using System.Linq;
    using NServiceBus.Transport.SqlServer.PubSub;
    using System.Threading;

    class SqlServerTransportInfrastructure : TransportInfrastructure
    {
        internal SqlServerTransportInfrastructure(SqlServerTransport transport, HostSettings hostSettings, QueueAddressTranslator addressTranslator, bool isEncrypted)
        {
            this.transport = transport;
            this.hostSettings = hostSettings;
            this.isEncrypted = isEncrypted;
            this.addressTranslator = addressTranslator;

            tableBasedQueueCache = new TableBasedQueueCache(addressTranslator, !isEncrypted);
            connectionFactory = CreateConnectionFactory();
        }

        public async Task ConfigureSubscriptions(string catalog, CancellationToken cancellationToken = default)
        {
            var pubSubSettings = transport.Subscriptions;
            var subscriptionStoreSchema = string.IsNullOrWhiteSpace(transport.DefaultSchema) ? "dbo" : transport.DefaultSchema;
            var subscriptionTableName = pubSubSettings.SubscriptionTableName.Qualify(subscriptionStoreSchema, catalog);

            subscriptionStore = new PolymorphicSubscriptionStore(new SubscriptionTable(subscriptionTableName.QuotedQualifiedName, connectionFactory));

            if (pubSubSettings.DisableCaching == false)
            {
                subscriptionStore = new CachedSubscriptionStore(subscriptionStore, pubSubSettings.CacheInvalidationPeriod);
            }

            if (hostSettings.SetupInfrastructure)
            {
                await new SubscriptionTableCreator(subscriptionTableName, connectionFactory).CreateIfNecessary(cancellationToken).ConfigureAwait(false);
            }

            transport.Testing.SubscriptionTable = subscriptionTableName.QuotedQualifiedName;
        }

        SqlConnectionFactory CreateConnectionFactory()
        {
            if (transport.ConnectionFactory != null)
            {
                return new SqlConnectionFactory(transport.ConnectionFactory);
            }

            return SqlConnectionFactory.Default(transport.ConnectionString);
        }

        public async Task ConfigureReceiveInfrastructure(ReceiveSettings[] receiveSettings, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            var transactionOptions = transport.TransactionScope.TransactionOptions;

            diagnostics.Add("NServiceBus.Transport.SqlServer.Transactions", new
            {
                TransactionMode = transport.TransportTransactionMode,
                transactionOptions.IsolationLevel,
                transactionOptions.Timeout
            });

            diagnostics.Add("NServiceBus.Transport.SqlServer.CircuitBreaker", new
            {
                TimeToWaitBeforeTriggering = transport.TimeToWaitBeforeTriggeringCircuitBreaker
            });

            var queuePeekerOptions = transport.QueuePeeker;

            var createMessageBodyComputedColumn = transport.CreateMessageBodyComputedColumn;

            Func<TransportTransactionMode, ProcessStrategy> processStrategyFactory =
                guarantee => SelectProcessStrategy(guarantee, transactionOptions, connectionFactory);

            var queuePurger = new QueuePurger(connectionFactory);
            var queuePeeker = new QueuePeeker(connectionFactory, queuePeekerOptions);

            IExpiredMessagesPurger expiredMessagesPurger;
            bool validateExpiredIndex;
            if (transport.ExpiredMessagesPurger.PurgeOnStartup == false)
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
                var purgeBatchSize = transport.ExpiredMessagesPurger.PurgeBatchSize;

                diagnostics.Add("NServiceBus.Transport.SqlServer.ExpiredMessagesPurger", new
                {
                    Enabled = true,
                    BatchSize = purgeBatchSize
                });

                expiredMessagesPurger = new ExpiredMessagesPurger((_, token) => connectionFactory.OpenNewConnection(token), purgeBatchSize);
                validateExpiredIndex = true;
            }

            var schemaVerification = new SchemaInspector((queue, token) => connectionFactory.OpenNewConnection(token), validateExpiredIndex);

            var queueFactory = transport.Testing.QueueFactoryOverride ?? (queueName => new TableBasedQueue(addressTranslator.Parse(queueName).QualifiedTableName, queueName, !isEncrypted));

            //Create delayed delivery infrastructure
            CanonicalQueueAddress delayedQueueCanonicalAddress = null;
            if (transport.DisableDelayedDelivery == false)
            {
                var delayedDelivery = transport.DelayedDelivery;

                diagnostics.Add("NServiceBus.Transport.SqlServer.DelayedDelivery", new
                {
                    Native = true,
                    Suffix = delayedDelivery.TableSuffix,
                    Interval = delayedDelivery.ProcessingInterval,
                    delayedDelivery.BatchSize,
                });

                var queueAddress = new Transport.QueueAddress(hostSettings.Name, null, new Dictionary<string, string>(), delayedDelivery.TableSuffix);

                delayedQueueCanonicalAddress = addressTranslator.GetCanonicalForm(addressTranslator.Generate(queueAddress));

                //For backwards-compatibility with previous version of the seam and endpoints that have delayed
                //delivery infrastructure, we assume that the first receiver address matches main input queue address
                //from version 7 of Core. For raw usages this will still work but delayed-delivery messages
                //might be moved to arbitrary picked receiver
                var mainReceiverInputQueueAddress = receiveSettings[0].ReceiveAddress;
                var inputQueueTable = addressTranslator.Parse(mainReceiverInputQueueAddress).QualifiedTableName;
                var delayedMessageTable = new DelayedMessageTable(delayedQueueCanonicalAddress.QualifiedTableName, inputQueueTable);

                //Allows dispatcher to store messages in the delayed store
                delayedMessageStore = delayedMessageTable;
                dueDelayedMessageProcessor = new DueDelayedMessageProcessor(delayedMessageTable, connectionFactory, delayedDelivery.ProcessingInterval, delayedDelivery.BatchSize, transport.TimeToWaitBeforeTriggeringCircuitBreaker, hostSettings);
            }

            Receivers = receiveSettings.Select(s =>
            {
                ISubscriptionManager subscriptionManager = transport.SupportsPublishSubscribe
                    ? (ISubscriptionManager)new SubscriptionManager(subscriptionStore, hostSettings.Name, s.ReceiveAddress)
                    : new NoOpSubscriptionManager();

                return new MessageReceiver(transport, s, hostSettings, processStrategyFactory, queueFactory, queuePurger,
                    expiredMessagesPurger,
                    queuePeeker, queuePeekerOptions, schemaVerification, transport.TimeToWaitBeforeTriggeringCircuitBreaker, subscriptionManager);

            }).ToDictionary<MessageReceiver, string, IMessageReceiver>(receiver => receiver.Id, receiver => receiver);

            await ValidateDatabaseAccess(transactionOptions, cancellationToken).ConfigureAwait(false);

            var receiveAddresses = receiveSettings.Select(r => r.ReceiveAddress).ToList();

            if (hostSettings.SetupInfrastructure)
            {
                var queuesToCreate = new List<string>();
                queuesToCreate.AddRange(sendingAddresses);
                queuesToCreate.AddRange(receiveAddresses);

                var queueCreator = new QueueCreator(connectionFactory, addressTranslator, createMessageBodyComputedColumn);

                await queueCreator.CreateQueueIfNecessary(queuesToCreate.ToArray(), delayedQueueCanonicalAddress, cancellationToken)
                    .ConfigureAwait(false);
            }

            dueDelayedMessageProcessor?.Start(cancellationToken);

            transport.Testing.SendingAddresses = sendingAddresses.Select(s => addressTranslator.Parse(s).QualifiedTableName).ToArray();
            transport.Testing.ReceiveAddresses = receiveAddresses.Select(r => addressTranslator.Parse(r).QualifiedTableName).ToArray();
            transport.Testing.DelayedDeliveryQueue = delayedQueueCanonicalAddress?.QualifiedTableName;
        }

        ProcessStrategy SelectProcessStrategy(TransportTransactionMode minimumConsistencyGuarantee, TransactionOptions options, SqlConnectionFactory connectionFactory)
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

        async Task ValidateDatabaseAccess(TransactionOptions transactionOptions, CancellationToken cancellationToken)
        {
            await TryOpenDatabaseConnection(cancellationToken).ConfigureAwait(false);

            await TryEscalateToDistributedTransactions(transactionOptions, cancellationToken).ConfigureAwait(false);
        }

        async Task TryOpenDatabaseConnection(CancellationToken cancellationToken)
        {
            try
            {
                using (await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                {
                }
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                var message = "Could not open connection to the SQL instance. Check the original error message for details. Original error message: " + ex.Message;

                throw new Exception(message, ex);
            }
        }

        async Task TryEscalateToDistributedTransactions(TransactionOptions transactionOptions, CancellationToken cancellationToken)
        {
            if (transport.TransportTransactionMode == TransportTransactionMode.TransactionScope)
            {
                var message = string.Empty;

                try
                {
                    using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                    using (await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                    using (await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
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
                    _logger.Warn(message);
                }
            }
        }

        public void ConfigureSendInfrastructure()
        {
            Dispatcher = new MessageDispatcher(
                addressTranslator,
                new MulticastToUnicastConverter(subscriptionStore),
                tableBasedQueueCache,
                delayedMessageStore,
                connectionFactory);
        }

        public override Task Shutdown(CancellationToken cancellationToken = default)
        {
            return dueDelayedMessageProcessor?.Stop(cancellationToken) ?? Task.FromResult(0);
        }

        readonly QueueAddressTranslator addressTranslator;
        readonly SqlServerTransport transport;
        readonly HostSettings hostSettings;
        DueDelayedMessageProcessor dueDelayedMessageProcessor;
        Dictionary<string, object> diagnostics = new Dictionary<string, object>();
        SqlConnectionFactory connectionFactory;
        ISubscriptionStore subscriptionStore;
        IDelayedMessageStore delayedMessageStore = new SendOnlyDelayedMessageStore();
        TableBasedQueueCache tableBasedQueueCache;
        bool isEncrypted;

        static ILog _logger = LogManager.GetLogger<SqlServerTransportInfrastructure>();
    }
}