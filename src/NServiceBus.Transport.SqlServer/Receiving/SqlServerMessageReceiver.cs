namespace NServiceBus.Transport.SqlServer;

using System;
using System.Threading;
using System.Threading.Tasks;
using Logging;
using Microsoft.Data.SqlClient;
using NServiceBus.Transport.Sql.Shared;
using Sql.Shared.Receiving;

class SqlServerMessageReceiver : MessageReceiver
{
    public SqlServerMessageReceiver(SqlServerTransport transport, string receiverId, string receiveAddress,
        string errorQueueAddress, Action<string, Exception, CancellationToken> criticalErrorAction,
        Func<TransportTransactionMode, ProcessStrategy> processStrategyFactory,
        Func<string, TableBasedQueue> queueFactory, IPurgeQueues queuePurger,
        IExpiredMessagesPurger expiredMessagesPurger, IPeekMessagesInQueue queuePeeker,
        SchemaInspector schemaInspector, TimeSpan waitTimeCircuitBreaker,
        ISubscriptionManager subscriptionManager, bool purgeAllMessagesOnStartup, IExceptionClassifier exceptionClassifier)
        : base(transport, receiverId,
        receiveAddress, errorQueueAddress, criticalErrorAction, processStrategyFactory, queueFactory, queuePurger,
        queuePeeker, waitTimeCircuitBreaker, subscriptionManager, purgeAllMessagesOnStartup, exceptionClassifier)
    {
        this.expiredMessagesPurger = expiredMessagesPurger;
        this.schemaInspector = schemaInspector;
    }

    protected override async Task PerformSchemaInspection(TableBasedQueue inputQueue,
        CancellationToken cancellationToken = default) =>
        await schemaInspector.PerformInspection(inputQueue, cancellationToken).ConfigureAwait(false);

    protected override async Task PurgeExpiredMessages(TableBasedQueue inputQueue,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await expiredMessagesPurger.Purge(inputQueue, cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException e) when (e.Number == 1205)
        {
            //Purge has been victim of a lock resolution
            Logger.Warn("Purger has been selected as a lock victim.", e);
        }
    }

    readonly IExpiredMessagesPurger expiredMessagesPurger;
    readonly SchemaInspector schemaInspector;

    static readonly ILog Logger = LogManager.GetLogger<SqlServerMessageReceiver>();
}