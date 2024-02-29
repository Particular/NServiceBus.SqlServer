namespace NServiceBus.Transport.PostgreSql;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Logging;
using Npgsql;
using SqlServer;
using SqlServer.PubSub;
using QueueAddress = QueueAddress;
using NServiceBus.Transport.Sql;
using Sql.Shared.Addressing;
using Sql.Shared.DelayedDelivery;
using Sql.Shared.PubSub;
using Sql.Shared.Queuing;
using Sql.Shared.Receiving;
using Sql.Shared.Sending;
using SqlTransport.DelayedDelivery;

class PostgreSqlTransportInfrastructure : TransportInfrastructure
{
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
    SqlServerDbConnectionFactory connectionFactory;
    ConnectionAttributes connectionAttributes;

    // TODO: Figure out if we should share this between SqlServer and PostgreSql in some static consts class or whatever
    static string DtcErrorMessage = @"
Distributed transactions are not available on Linux. The other transaction modes can be used by setting the `PostgreSqlTransport.TransportTransactionMode` property when configuring the endpoint.
Be aware that different transaction modes affect consistency guarantees since distributed transactions won't be atomically updating the resources together with consuming the incoming message.";

    static ILog _logger = LogManager.GetLogger<PostgreSqlTransportInfrastructure>();
    readonly PostgreSqlNameHelper nameHelper;

    public PostgreSqlTransportInfrastructure(PostgreSqlTransport transport, HostSettings hostSettings,
        ReceiveSettings[] receiveSettings, string[] sendingAddresses)
    {
        this.transport = transport;
        this.hostSettings = hostSettings;
        this.receiveSettings = receiveSettings;
        this.sendingAddresses = sendingAddresses;

        sqlConstants = new PostgreSqlConstants();
        nameHelper = new PostgreSqlNameHelper();
    }

    public override Task Shutdown(CancellationToken cancellationToken = default)
    {
        return dueDelayedMessageProcessor?.Stop(cancellationToken) ?? Task.FromResult(0);
    }

    public override string ToTransportAddress(QueueAddress address)
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
        connectionFactory = new SqlServerDbConnectionFactory(async ct =>
        {
            var connection = new NpgsqlConnection(transport.ConnectionString);

            await connection.OpenAsync(ct).ConfigureAwait(false);

            return connection;
        });

        connectionAttributes = ConnectionAttributesParser.Parse(transport.ConnectionString, transport.DefaultCatalog);

        addressTranslator = new QueueAddressTranslator(connectionAttributes.Catalog, "public", transport.DefaultSchema, transport.SchemaAndCatalog, nameHelper);
        //TODO: check if we can provide streaming capability with PostgreSql
        tableBasedQueueCache = new TableBasedQueueCache(
            (qualifiedTableName, queueName, isStreamSupported) => new PostgreSqlTableBasedQueue(sqlConstants, qualifiedTableName, queueName, isStreamSupported),
            addressTranslator,
            false);

        await ConfigureSubscriptions(cancellationToken).ConfigureAwait(false);

        await ConfigureReceiveInfrastructure(cancellationToken).ConfigureAwait(false);

        ConfigureSendInfrastructure();
    }

    void ConfigureSendInfrastructure()
    {
        Dispatcher = new MessageDispatcher(
            addressTranslator,
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
        var queuePeeker = new QueuePeeker(connectionFactory, queuePeekerOptions.Delay);

        IExpiredMessagesPurger expiredMessagesPurger;
        bool validateExpiredIndex;

        // TODO: Figure out what to do here, MessageReceiver needs a purger
        expiredMessagesPurger = new NoOpExpiredMessagesPurger();
        validateExpiredIndex = false;
        // if (transport.ExpiredMessagesPurger.PurgeOnStartup == false)
        // {
        //     diagnostics.Add("NServiceBus.Transport.SqlServer.ExpiredMessagesPurger", new { Enabled = false, });
        //     expiredMessagesPurger = new NoOpExpiredMessagesPurger();
        //     validateExpiredIndex = false;
        // }
        // else
        // {
        //     var purgeBatchSize = transport.ExpiredMessagesPurger.PurgeBatchSize;
        //
        //     diagnostics.Add("NServiceBus.Transport.SqlServer.ExpiredMessagesPurger",
        //         new { Enabled = true, BatchSize = purgeBatchSize });
        //
        //     expiredMessagesPurger = new ExpiredMessagesPurger((_, token) => connectionFactory.OpenNewConnection(token),
        //         purgeBatchSize);
        //     validateExpiredIndex = true;
        // }

        var schemaVerification = new SchemaInspector((queue, token) => connectionFactory.OpenNewConnection(token),
            validateExpiredIndex);

        var queueFactory = transport.Testing.QueueFactoryOverride ?? (queueName => new PostgreSqlTableBasedQueue(sqlConstants,
            addressTranslator.Parse(queueName).QualifiedTableName, queueName, !connectionAttributes.IsEncrypted));

        //Create delayed delivery infrastructure
        CanonicalQueueAddress delayedQueueCanonicalAddress = null;
        if (transport.DisableDelayedDelivery == false)
        {
            var delayedDelivery = transport.DelayedDelivery;

            // diagnostics.Add("NServiceBus.Transport.SqlServer.DelayedDelivery",
            //     new { Native = true, Suffix = delayedDelivery.TableSuffix, delayedDelivery.BatchSize, });

            var queueAddress = new QueueAddress(hostSettings.Name, null, new Dictionary<string, string>(),
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
            dueDelayedMessageProcessor = new DueDelayedMessageProcessor(delayedMessageTable, connectionFactory,
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
                queuePeeker, queuePeekerOptions, transport.TimeToWaitBeforeTriggeringCircuitBreaker,
                subscriptionManager, receiveSetting.PurgeOnStartup);
        }).ToDictionary<MessageReceiver, string, IMessageReceiver>(receiver => receiver.Id, receiver => receiver);

        await ValidateDatabaseAccess(transactionOptions, cancellationToken).ConfigureAwait(false);

        var receiveAddresses = Receivers.Values.Select(r => r.ReceiveAddress).ToList();

        if (hostSettings.SetupInfrastructure)
        {
            var queuesToCreate = new List<string>();
            queuesToCreate.AddRange(sendingAddresses);
            queuesToCreate.AddRange(receiveAddresses);

            var queueCreator = new QueueCreator(sqlConstants, connectionFactory, addressTranslator,
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
    async Task ValidateDatabaseAccess(TransactionOptions transactionOptions, CancellationToken cancellationToken)
    {
        await TryOpenDatabaseConnection(cancellationToken).ConfigureAwait(false);

        await TryEscalateToDistributedTransactions(transactionOptions, cancellationToken).ConfigureAwait(false);
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
    async Task TryEscalateToDistributedTransactions(TransactionOptions transactionOptions,
        CancellationToken cancellationToken)
    {
        if (transport.TransportTransactionMode == TransportTransactionMode.TransactionScope)
        {
            var message = string.Empty;

            try
            {
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions,
                           TransactionScopeAsyncFlowOption.Enabled))
                {
                    FakePromotableResourceManager.ForceDtc();
                    using (await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                    {
                        scope.Complete();
                    }
                }
            }
            catch (NotSupportedException exception)
            {
                message =
                    "The version of the SqlClient in use does not support enlisting SQL connections in distributed transactions."
                    + DtcErrorMessage + "Original error message: " + exception.Message;
            }
            catch (Exception exception) when (!exception.IsCausedBy(cancellationToken))
            {
                message = "Distributed transactions are not available."
                          + DtcErrorMessage + "Original error message: " + exception.Message;
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                _logger.Warn(message);
            }
        }
    }

    // TODO: Make this thing shared for both transports
    ProcessStrategy SelectProcessStrategy(TransportTransactionMode minimumConsistencyGuarantee,
        TransactionOptions options, SqlServerDbConnectionFactory connectionFactory)
    {
        if (minimumConsistencyGuarantee == TransportTransactionMode.TransactionScope)
        {
            return new ProcessWithTransactionScope(options, connectionFactory, new FailureInfoStorage(10000),
                tableBasedQueueCache);
        }

        if (minimumConsistencyGuarantee == TransportTransactionMode.SendsAtomicWithReceive)
        {
            return new ProcessWithNativeTransaction(options, connectionFactory, new FailureInfoStorage(10000),
                tableBasedQueueCache);
        }

        if (minimumConsistencyGuarantee == TransportTransactionMode.ReceiveOnly)
        {
            return new ProcessWithNativeTransaction(options, connectionFactory, new FailureInfoStorage(10000),
                tableBasedQueueCache, transactionForReceiveOnly: true);
        }

        return new ProcessWithNoTransaction(connectionFactory, tableBasedQueueCache);
    }


    async Task ConfigureSubscriptions(CancellationToken cancellationToken)
    {
        subscriptionStore = new SubscriptionStore();
        var pubSubSettings = transport.Subscriptions;
        var subscriptionStoreSchema =
            string.IsNullOrWhiteSpace(transport.DefaultSchema) ? "public" : transport.DefaultSchema;
        var subscriptionTableName =
            pubSubSettings.SubscriptionTableName.Qualify(subscriptionStoreSchema, connectionAttributes.Catalog, nameHelper);

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

        //transport.Testing.SubscriptionTable = subscriptionTableName.QuotedQualifiedName;
    }
}