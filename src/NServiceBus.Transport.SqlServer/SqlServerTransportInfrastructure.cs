using System.Linq;
using NServiceBus.Transport.SqlServer.PubSub;

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

        public Task ConfigureSubscriptions(HostSettings hostSettings, string catalog)
        {
            var pubSubSettings = transport.Subscriptions;
            var subscriptionTableName = pubSubSettings.SubscriptionTable.Qualify(string.IsNullOrWhiteSpace(transport.DefaultSchema) ? "dbo" : transport.DefaultSchema, catalog);
            
            subscriptionStore = new PolymorphicSubscriptionStore(new SubscriptionTable(subscriptionTableName.QuotedQualifiedName, connectionFactory));
            var timeToCacheSubscriptions = pubSubSettings.TimeToCacheSubscriptions;
            if (timeToCacheSubscriptions.HasValue)
            {
                subscriptionStore = new CachedSubscriptionStore(subscriptionStore, timeToCacheSubscriptions.Value);
            }

            if (hostSettings.SetupInfrastructure)
            {
                return new SubscriptionTableCreator(subscriptionTableName, connectionFactory).CreateIfNecessary();
            }

            transport.Testing.SubscriptionTable = subscriptionTableName.QuotedQualifiedName;

            return Task.CompletedTask;
        }

        SqlConnectionFactory CreateConnectionFactory()
        {
            if (transport.ConnectionFactory != null)
            {
                return new SqlConnectionFactory(transport.ConnectionFactory);
            }

            return SqlConnectionFactory.Default(transport.ConnectionString);
        }

        public async Task ConfigureReceiveInfrastructure(ReceiveSettings[] receiveSettings, string[] sendingAddresses)
        {
            var scopeOptions = transport.ScopeOptions;

            diagnostics.Add("NServiceBus.Transport.SqlServer.Transactions", new
            {
                TransactionMode = transport.TransportTransactionMode,
                scopeOptions.TransactionOptions.IsolationLevel,
                scopeOptions.TransactionOptions.Timeout
            });

            diagnostics.Add("NServiceBus.Transport.SqlServer.CircuitBreaker", new
            {
                 transport.TimeToWaitBeforeTriggering
            });

            var queuePeekerOptions = transport.QueuePeekerOptions;

            var createMessageBodyComputedColumn = transport.CreateMessageBodyComputedColumn;

            Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory =
                guarantee => SelectReceiveStrategy(guarantee, scopeOptions.TransactionOptions, connectionFactory);

            var queuePurger = new QueuePurger(connectionFactory);
            var queuePeeker = new QueuePeeker(connectionFactory, queuePeekerOptions);

            IExpiredMessagesPurger expiredMessagesPurger;
            bool validateExpiredIndex;
            if (transport.PurgeExpiredMessagesOnStartup == false)
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
                var purgeBatchSize = transport.PurgeExpiredMessagesBatchSize;

                diagnostics.Add("NServiceBus.Transport.SqlServer.ExpiredMessagesPurger", new
                {
                    Enabled = true,
                    BatchSize = purgeBatchSize
                });

                expiredMessagesPurger = new ExpiredMessagesPurger(_ => connectionFactory.OpenNewConnection(), purgeBatchSize);
                validateExpiredIndex = true;
            }

            var schemaVerification = new SchemaInspector(queue => connectionFactory.OpenNewConnection(), validateExpiredIndex);

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
                    BatchSize = delayedDelivery.BatchSize,
                });

                var queueAddress = new Transport.QueueAddress(hostSettings.Name, null, new Dictionary<string, string>(), delayedDelivery.TableSuffix);

                delayedQueueCanonicalAddress = addressTranslator.GetCanonicalForm(addressTranslator.Generate(queueAddress));

                //For backwards-compatibility with previous version of the seam and endpoints that have delayed
                //delivery infrastructure, we assume that the first receiver address matches main input queue address
                //from version 7 of Core
                var mainReceiverInputQueueAddress = receiveSettings[0].ReceiveAddress;
                var inputQueueTable = addressTranslator.Parse(mainReceiverInputQueueAddress).QualifiedTableName;
                var delayedMessageTable = new DelayedMessageTable(delayedQueueCanonicalAddress.QualifiedTableName, inputQueueTable);

                //Allows dispatcher to store messages in the delayed store
                delayedMessageStore = delayedMessageTable;
                dueDelayedMessageProcessor = new DueDelayedMessageProcessor(delayedMessageTable, connectionFactory, delayedDelivery.ProcessingInterval, delayedDelivery.BatchSize);
            }

            Receivers = receiveSettings.Select(s =>
            {
                ISubscriptionManager subscriptionManager = transport.SupportsPublishSubscribe
                    ? (ISubscriptionManager) new SubscriptionManager(subscriptionStore, hostSettings.Name, s.ReceiveAddress)
                    : new NoOpSubscriptionManager();

                return new MessagePump(transport, s, hostSettings, receiveStrategyFactory, queueFactory, queuePurger,
                    expiredMessagesPurger,
                    queuePeeker, queuePeekerOptions, schemaVerification, transport.TimeToWaitBeforeTriggering, subscriptionManager);
                
            }).ToList<IMessageReceiver>().AsReadOnly();

            await ValidateDatabaseAccess(scopeOptions.TransactionOptions).ConfigureAwait(false);

            dueDelayedMessageProcessor?.Start();

            var receiveAddresses = receiveSettings.Select(r => r.ReceiveAddress).ToList();

            if (hostSettings.SetupInfrastructure)
            {
                var queuesToCreate = new List<string>();
                queuesToCreate.AddRange(sendingAddresses);
                queuesToCreate.AddRange(receiveAddresses);

                var queueCreator = new QueueCreator(connectionFactory, addressTranslator, createMessageBodyComputedColumn);

                await queueCreator.CreateQueueIfNecessary(queuesToCreate.ToArray(), delayedQueueCanonicalAddress)
                    .ConfigureAwait(false);
            }

            transport.Testing.SendingAddresses = sendingAddresses.Select(s => addressTranslator.Parse(s).QualifiedTableName).ToArray();
            transport.Testing.ReceiveAddresses = receiveAddresses.Select(r => addressTranslator.Parse(r).QualifiedTableName).ToArray();
            transport.Testing.DelayedDeliveryQueue = delayedQueueCanonicalAddress?.QualifiedTableName;
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

        async Task ValidateDatabaseAccess(TransactionOptions transactionOptions)
        {
            await TryOpenDatabaseConnection().ConfigureAwait(false);

            await TryEscalateToDistributedTransactions(transactionOptions).ConfigureAwait(false);
        }

        async Task TryOpenDatabaseConnection()
        {
            try
            {
                using (await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                }
            }
            catch (Exception ex)
            {
                var message = "Could not open connection to the SQL instance. Check the original error message for details. Original error message: " + ex.Message;

                throw new Exception(message, ex);
            }
        }

        async Task TryEscalateToDistributedTransactions(TransactionOptions transactionOptions)
        {
            if (transport.TransportTransactionMode == TransportTransactionMode.TransactionScope)
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

        public override Task DisposeAsync()
        {
            return dueDelayedMessageProcessor?.Stop() ?? Task.FromResult(0);
        }

        internal QueueAddressTranslator addressTranslator;
        readonly SqlServerTransport transport;
        readonly HostSettings hostSettings;
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