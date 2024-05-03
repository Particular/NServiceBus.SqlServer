namespace NServiceBus.Transport.PostgreSql;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Logging;
using PubSub;
using Sql.Shared.Configuration;
using SqlServer;
using Sql.Shared.DelayedDelivery;
using Sql.Shared.PubSub;
using Sql.Shared.Queuing;
using Sql.Shared.Receiving;
using Sql.Shared.Sending;

class PostgreSqlTransportInfrastructure : TransportInfrastructure
{
    //The PostgreSQL limit is 63 but we need to reserves space for "_Seq_seq" suffix used in the
    //auto-created sequence and that is 8 bytes less
    const int TableQueueNameLimit = 55;
    readonly PostgreSqlTransport transport;
    readonly HostSettings hostSettings;
    readonly ReceiveSettings[] receiveSettings;
    readonly string[] sendingAddresses;
    readonly PostgreSqlConstants sqlConstants;

    DueDelayedMessageProcessor dueDelayedMessageProcessor;
    QueueAddressTranslator addressTranslator;
    TableBasedQueueCache tableBasedQueueCache;
    ISubscriptionStore subscriptionStore;
    IDelayedMessageStore delayedMessageStore = new SendOnlyDelayedMessageStore();
    PostgreSqlDbConnectionFactory connectionFactory;

    // TODO: Figure out if we should share this between SqlServer and PostgreSql in some static consts class or whatever
    //    static string DtcErrorMessage = @"
    //Distributed transactions are not available on Linux. The other transaction modes can be used by setting the `PostgreSqlTransport.TransportTransactionMode` property when configuring the endpoint.
    //Be aware that different transaction modes affect consistency guarantees since distributed transactions won't be atomically updating the resources together with consuming the incoming message.";

    static ILog _logger = LogManager.GetLogger<PostgreSqlTransportInfrastructure>();
    readonly PostgreSqlNameHelper nameHelper;
    readonly PostgreSqlExceptionClassifier exceptionClassifier;

    public PostgreSqlTransportInfrastructure(PostgreSqlTransport transport, HostSettings hostSettings,
        ReceiveSettings[] receiveSettings, string[] sendingAddresses)
    {
        this.transport = transport;
        this.hostSettings = hostSettings;
        this.receiveSettings = receiveSettings;
        this.sendingAddresses = sendingAddresses;

        sqlConstants = new PostgreSqlConstants();
        nameHelper = new PostgreSqlNameHelper();
        exceptionClassifier = new PostgreSqlExceptionClassifier();
    }

    public override Task Shutdown(CancellationToken cancellationToken = default)
    {
        return dueDelayedMessageProcessor?.Stop(cancellationToken) ?? Task.FromResult(0);
    }

    public override string ToTransportAddress(Transport.QueueAddress address)
    {
        if (addressTranslator == null)
        {
            throw new Exception("Initialize must be called before using ToTransportAddress");
        }

        var tableQueueAddress = addressTranslator.Generate(address);

        return addressTranslator.GetCanonicalForm(tableQueueAddress).Address;
    }

    public async Task Initialize(CancellationToken cancellationToken = new())
    {
        connectionFactory = CreateConnectionFactory();

        addressTranslator = new QueueAddressTranslator("public", transport.DefaultSchema, transport.Schema, nameHelper);
        //TODO: check if we can provide streaming capability with PostgreSql
        tableBasedQueueCache = new TableBasedQueueCache(
            (address, isStreamSupported) =>
            {
                var canonicalAddress = addressTranslator.Parse(address);
                return new PostgreSqlTableBasedQueue(sqlConstants, canonicalAddress.QualifiedTableName, canonicalAddress.Address);
            },
            s => addressTranslator.Parse(s).Address,
            false);

        await ConfigureSubscriptions(cancellationToken).ConfigureAwait(false);

        await ConfigureReceiveInfrastructure(cancellationToken).ConfigureAwait(false);

        ConfigureSendInfrastructure();
    }

    PostgreSqlDbConnectionFactory CreateConnectionFactory()
    {
        if (transport.ConnectionFactory != null)
        {
            return new PostgreSqlDbConnectionFactory(async (ct) => await transport.ConnectionFactory(ct).ConfigureAwait(false));
        }

        return new PostgreSqlDbConnectionFactory(transport.ConnectionString);
    }


    void ConfigureSendInfrastructure()
    {
        Dispatcher = new MessageDispatcher(
            s => addressTranslator.Parse(s).Address,
            new MulticastToUnicastConverter(subscriptionStore),
            tableBasedQueueCache,
            delayedMessageStore,
            connectionFactory);
    }

    async Task ConfigureReceiveInfrastructure(CancellationToken cancellationToken)
    {
        if (receiveSettings.Length == 0)
        {
            Receivers = new Dictionary<string, IMessageReceiver>();
            return;
        }

        var transactionOptions = transport.TransactionScope.TransactionOptions;

        // diagnostics.Add("NServiceBus.Transport.SqlServer.Transactions",
        //     new
        //     {
        //         TransactionMode = transport.TransportTransactionMode,
        //         transactionOptions.IsolationLevel,
        //         transactionOptions.Timeout
        //     });

        // diagnostics.Add("NServiceBus.Transport.SqlServer.CircuitBreaker",
        //     new { TimeToWaitBeforeTriggering = transport.TimeToWaitBeforeTriggeringCircuitBreaker });

        var queuePeekerOptions = transport.QueuePeeker;

        var createMessageBodyComputedColumn = transport.CreateMessageBodyComputedColumn;

        Func<TransportTransactionMode, ProcessStrategy> processStrategyFactory =
            guarantee => SelectProcessStrategy(guarantee, transactionOptions, connectionFactory);

        var queuePurger = new QueuePurger(connectionFactory);
        var queuePeeker = new QueuePeeker(connectionFactory, exceptionClassifier, queuePeekerOptions.Delay);

        var queueFactory = new Func<string, PostgreSqlTableBasedQueue>(queueName => new PostgreSqlTableBasedQueue(sqlConstants,
            addressTranslator.Parse(queueName).QualifiedTableName, queueName));

        //Create delayed delivery infrastructure
        CanonicalQueueAddress delayedQueueCanonicalAddress = null;
        if (transport.DisableDelayedDelivery == false)
        {
            var delayedDelivery = transport.DelayedDelivery;

            // diagnostics.Add("NServiceBus.Transport.SqlServer.DelayedDelivery",
            //     new { Native = true, Suffix = delayedDelivery.TableSuffix, delayedDelivery.BatchSize, });

            var queueAddress = new Transport.QueueAddress(hostSettings.Name, null, new Dictionary<string, string>(),
                delayedDelivery.TableSuffix);

            delayedQueueCanonicalAddress = addressTranslator.GetCanonicalForm(addressTranslator.Generate(queueAddress));

            //For backwards-compatibility with previous version of the seam and endpoints that have delayed
            //delivery infrastructure, we assume that the first receiver address matches main input queue address
            //from version 7 of Core. For raw usages this will still work but delayed-delivery messages
            //might be moved to arbitrary picked receiver
            var mainReceiverInputQueueAddress = ToTransportAddress(receiveSettings[0].ReceiveAddress);

            var inputQueueTable = addressTranslator.Parse(mainReceiverInputQueueAddress).QualifiedTableName;
            var delayedMessageTable = new DelayedMessageTable(sqlConstants,
                delayedQueueCanonicalAddress.QualifiedTableName, inputQueueTable);

            //Allows dispatcher to store messages in the delayed store
            delayedMessageStore = delayedMessageTable;
            dueDelayedMessageProcessor = new DueDelayedMessageProcessor(delayedMessageTable, connectionFactory, exceptionClassifier,
                delayedDelivery.BatchSize, transport.TimeToWaitBeforeTriggeringCircuitBreaker, hostSettings);
        }

        Receivers = receiveSettings.Select(receiveSetting =>
        {
            var receiveAddress = ToTransportAddress(receiveSetting.ReceiveAddress);
            ISubscriptionManager subscriptionManager = transport.SupportsPublishSubscribe
                ? new SubscriptionManager(subscriptionStore, hostSettings.Name, receiveAddress)
                : new NoOpSubscriptionManager();

            if (receiveSetting.PurgeOnStartup)
            {
                _logger.Warn($"The {receiveSetting.PurgeOnStartup} should only be used in the development environment.");
            }

            return new PostgreSqlMessageReceiver(transport, receiveSetting.Id, receiveAddress, receiveSetting.ErrorQueue,
                hostSettings.CriticalErrorAction, processStrategyFactory, queueFactory, queuePurger,
                queuePeeker, transport.TimeToWaitBeforeTriggeringCircuitBreaker,
                subscriptionManager, receiveSetting.PurgeOnStartup, exceptionClassifier);
        }).ToDictionary<MessageReceiver, string, IMessageReceiver>(receiver => receiver.Id, receiver => receiver);

        await ValidateDatabaseAccess(transactionOptions, cancellationToken).ConfigureAwait(false);

        var receiveAddresses = Receivers.Values.Select(r => r.ReceiveAddress).ToList();

        if (hostSettings.SetupInfrastructure)
        {
            var queuesToCreate = new List<string>();
            queuesToCreate.AddRange(sendingAddresses);
            queuesToCreate.AddRange(receiveAddresses);

            var queueNameExceedsLimit = queuesToCreate.Any(q => Encoding.UTF8.GetBytes(QueueAddress.Parse(q, nameHelper).Table).Length > TableQueueNameLimit);

            var delayedQueueNameExceedsLimit =
                Encoding.UTF8.GetBytes(delayedQueueCanonicalAddress.Table).Length > TableQueueNameLimit;

            if (queueNameExceedsLimit || delayedQueueNameExceedsLimit)
            {
                throw new Exception($"PostgreSQL table name exceeds {TableQueueNameLimit} in length, consider using shorter endpoint names");
            }

            var queueCreator = new QueueCreator(sqlConstants, connectionFactory, addressTranslator.Parse,
                createMessageBodyComputedColumn);

            await queueCreator.CreateQueueIfNecessary(queuesToCreate.ToArray(), delayedQueueCanonicalAddress,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        dueDelayedMessageProcessor?.Start(cancellationToken);

        transport.Testing.SendingAddresses =
            sendingAddresses.Select(s => addressTranslator.Parse(s).QualifiedTableName).ToArray();
        transport.Testing.ReceiveAddresses =
            receiveAddresses.Select(r => addressTranslator.Parse(r).QualifiedTableName).ToArray();
        transport.Testing.DelayedDeliveryQueue = delayedQueueCanonicalAddress?.QualifiedTableName;
    }

    // TODO: Make this thing shared for both transports
#pragma warning disable IDE0060
    async Task ValidateDatabaseAccess(TransactionOptions transactionOptions, CancellationToken cancellationToken)
#pragma warning restore IDE0060
    {
        await TryOpenDatabaseConnection(cancellationToken).ConfigureAwait(false);


        //TODO: Figure out if PostgreSQL supports DTC
        //await TryEscalateToDistributedTransactions(transactionOptions, cancellationToken).ConfigureAwait(false);
    }

    // TODO: Make this thing shared for both transports
    async Task TryOpenDatabaseConnection(CancellationToken cancellationToken)
    {
        try
        {
            await using (await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
            }
        }
        catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
        {
            var message =
                "Could not open connection to the PostgreSql instance. Check the original error message for details. Original error message: " +
                ex.Message;

            throw new Exception(message, ex);
        }
    }

    // TODO: Make this thing shared for both transports
    //async Task TryEscalateToDistributedTransactions(TransactionOptions transactionOptions,
    //    CancellationToken cancellationToken)
    //{
    //    if (transport.TransportTransactionMode == TransportTransactionMode.TransactionScope)
    //    {
    //        var message = string.Empty;

    //        try
    //        {
    //            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions,
    //                       TransactionScopeAsyncFlowOption.Enabled))
    //            {
    //                FakePromotableResourceManager.ForceDtc();
    //                using (await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
    //                {
    //                    scope.Complete();
    //                }
    //            }
    //        }
    //        catch (NotSupportedException exception)
    //        {
    //            message =
    //                "The version of the SqlClient in use does not support enlisting SQL connections in distributed transactions."
    //                + DtcErrorMessage + "Original error message: " + exception.Message;
    //        }
    //        catch (Exception exception) when (!exception.IsCausedBy(cancellationToken))
    //        {
    //            message = "Distributed transactions are not available."
    //                      + DtcErrorMessage + "Original error message: " + exception.Message;
    //        }

    //        if (!string.IsNullOrWhiteSpace(message))
    //        {
    //            _logger.Warn(message);
    //        }
    //    }
    //}

    // TODO: Make this thing shared for both transports
    ProcessStrategy SelectProcessStrategy(TransportTransactionMode minimumConsistencyGuarantee,
        TransactionOptions options, DbConnectionFactory connectionFactory)
    {
        var failureInfoStorage = new FailureInfoStorage(10000);

        if (minimumConsistencyGuarantee == TransportTransactionMode.TransactionScope)
        {
            return new ProcessWithTransactionScope(options, connectionFactory, failureInfoStorage,
                tableBasedQueueCache, exceptionClassifier);
        }

        if (minimumConsistencyGuarantee == TransportTransactionMode.SendsAtomicWithReceive)
        {
            return new ProcessWithNativeTransaction(options, connectionFactory, failureInfoStorage,
                tableBasedQueueCache, exceptionClassifier);
        }

        if (minimumConsistencyGuarantee == TransportTransactionMode.ReceiveOnly)
        {
            return new ProcessWithNativeTransaction(options, connectionFactory, failureInfoStorage,
                tableBasedQueueCache, exceptionClassifier, transactionForReceiveOnly: true);
        }

        return new ProcessWithNoTransaction(connectionFactory, failureInfoStorage, tableBasedQueueCache, exceptionClassifier);
    }


    async Task ConfigureSubscriptions(CancellationToken cancellationToken)
    {
        subscriptionStore = new SubscriptionStore();
        var pubSubSettings = transport.Subscriptions;
        var subscriptionStoreSchema =
            string.IsNullOrWhiteSpace(transport.DefaultSchema) ? "public" : transport.DefaultSchema;
        var subscriptionTableName =
            pubSubSettings.SubscriptionTableName.Qualify(subscriptionStoreSchema, nameHelper);

        subscriptionStore = new PolymorphicSubscriptionStore(new SubscriptionTable(sqlConstants,
            subscriptionTableName.QuotedQualifiedName, connectionFactory));

        if (pubSubSettings.DisableCaching == false)
        {
            subscriptionStore = new CachedSubscriptionStore(subscriptionStore, pubSubSettings.CacheInvalidationPeriod);
        }

        if (hostSettings.SetupInfrastructure)
        {
            await new SubscriptionTableCreator(sqlConstants, subscriptionTableName, connectionFactory)
                .CreateIfNecessary(cancellationToken).ConfigureAwait(false);
        }

        transport.Testing.SubscriptionTable = subscriptionTableName.QuotedQualifiedName;
    }
}