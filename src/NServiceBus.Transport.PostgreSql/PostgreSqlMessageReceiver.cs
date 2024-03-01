namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Sql.Shared;
    using Sql.Shared.Queuing;
    using Sql.Shared.Receiving;

    class PostgreSqlMessageReceiver : MessageReceiver
    {
        public PostgreSqlMessageReceiver(PostgreSqlTransport transport, string receiverId, string receiveAddress,
            string errorQueueAddress, Action<string, Exception, CancellationToken> criticalErrorAction,
            Func<TransportTransactionMode, ProcessStrategy> processStrategyFactory,
            Func<string, TableBasedQueue> queueFactory, IPurgeQueues queuePurger,
            IPeekMessagesInQueue queuePeeker, TimeSpan waitTimeCircuitBreaker,
            ISubscriptionManager subscriptionManager, bool purgeAllMessagesOnStartup, IExceptionClassifier exceptionClassifier) : base(transport, receiverId,
            receiveAddress, errorQueueAddress, criticalErrorAction, processStrategyFactory, queueFactory, queuePurger,
            queuePeeker, waitTimeCircuitBreaker,
            subscriptionManager, purgeAllMessagesOnStartup, exceptionClassifier)
        {
        }

        protected override Task PerformSchemaInspection(TableBasedQueue inputQueue,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        protected override Task PurgeExpiredMessages(TableBasedQueue inputQueue,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}