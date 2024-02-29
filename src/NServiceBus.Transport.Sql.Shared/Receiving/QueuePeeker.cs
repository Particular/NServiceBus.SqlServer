namespace NServiceBus.Transport.Sql.Shared.Receiving
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Configuration;
    using Logging;
    using Queuing;

    public class QueuePeeker : IPeekMessagesInQueue
    {
        public QueuePeeker(DbConnectionFactory connectionFactory, TimeSpan peekDelay)
        {
            this.connectionFactory = connectionFactory;
            this.peekDelay = peekDelay;
        }

        public async Task<int> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default)
        {
            var messageCount = 0;

            try
            {
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
                using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                {
                    messageCount = await inputQueue.TryPeek(connection, null, cancellationToken: cancellationToken).ConfigureAwait(false);

                    scope.Complete();
                }

                circuitBreaker.Success();
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Logger.Warn("Sql peek operation failed", ex);
                await circuitBreaker.Failure(ex, cancellationToken).ConfigureAwait(false);
            }

            if (messageCount == 0)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Input queue empty. Next peek operation will be delayed for {peekDelay}.");
                }

                await Task.Delay(peekDelay, cancellationToken).ConfigureAwait(false);
            }

            return messageCount;
        }

        readonly DbConnectionFactory connectionFactory;
        readonly TimeSpan peekDelay;

        static readonly ILog Logger = LogManager.GetLogger<QueuePeeker>();
    }
}