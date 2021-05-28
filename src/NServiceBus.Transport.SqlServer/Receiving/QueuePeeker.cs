namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Logging;

    class QueuePeeker : IPeekMessagesInQueue
    {
        public QueuePeeker(SqlConnectionFactory connectionFactory, QueuePeekerOptions settings)
        {
            this.connectionFactory = connectionFactory;
            this.settings = settings;
        }

        public async Task<int> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default)
        {
            var messageCount = 0;

            try
            {
#if NETFRAMEWORK
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
                using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                {
                    messageCount = await inputQueue.TryPeek(connection, null, cancellationToken: cancellationToken).ConfigureAwait(false);

                    scope.Complete();
                }

#else
                using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
                using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                using (var tx = connection.BeginTransaction())
                {
                    messageCount = await inputQueue.TryPeek(connection, tx, cancellationToken: cancellationToken).ConfigureAwait(false);

                    tx.Commit();
                    scope.Complete();
                }
#endif

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
                    Logger.Debug($"Input queue empty. Next peek operation will be delayed for {settings.Delay}.");
                }

                await Task.Delay(settings.Delay, cancellationToken).ConfigureAwait(false);
            }

            return messageCount;
        }

        readonly SqlConnectionFactory connectionFactory;
        readonly QueuePeekerOptions settings;

        static readonly ILog Logger = LogManager.GetLogger<QueuePeeker>();
    }
}