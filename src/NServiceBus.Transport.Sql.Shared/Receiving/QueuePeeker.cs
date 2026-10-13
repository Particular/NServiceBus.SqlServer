namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Logging;

    class QueuePeeker(DbConnectionFactory connectionFactory, IExceptionClassifier exceptionClassifier, TimeSpan peekDelay) : IPeekMessagesInQueue
    {
        public async Task<PeekResult> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default)
        {
            var peekResult = PeekResult.Empty;

            try
            {
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
                using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                {
                    peekResult = await inputQueue.TryPeek(connection, null, cancellationToken: cancellationToken).ConfigureAwait(false);

                    scope.Complete();
                }

                circuitBreaker.Success();
            }
            catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
            {
                Logger.Warn("Sql peek operation failed", ex);
                await circuitBreaker.Failure(ex, cancellationToken).ConfigureAwait(false);
            }

            return peekResult;
        }

        public Task WaitForPeekDelay(CancellationToken cancellationToken = default)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Input queue empty. Next peek operation will be delayed for {peekDelay}.");
            }

            return Task.Delay(peekDelay, cancellationToken);
        }

        static readonly ILog Logger = LogManager.GetLogger<QueuePeeker>();
    }
}