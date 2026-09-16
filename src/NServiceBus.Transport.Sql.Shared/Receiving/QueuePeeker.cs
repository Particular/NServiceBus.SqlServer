namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Logging;

    class QueuePeeker(DbConnectionFactory connectionFactory, IExceptionClassifier exceptionClassifier, TimeSpan peekDelay) : IPeekMessagesInQueue
    {
        public async Task<int> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default)
        {
            var messageCount = 0;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
                // WORKAROUND: opening the connection is deliberately not cancellable.
                //
                // Cancelling an OpenAsync that is auto enlisting in this scope's transaction
                // deadlocks inside Microsoft.Data.SqlClient. The client finishes the open the caller
                // has already abandoned and enlists it on one thread, while the dispose below rolls
                // the transaction back on another, and the two take the connection monitor and the
                // parser lock in opposite orders. This peek then never completes, so
                // MessageReceiver.StopReceive waits on it forever and the endpoint never shuts down.
                //
                // TODO: undo this workaround, passing cancellationToken to OpenNewConnection again,
                // once https://github.com/dotnet/SqlClient/issues/4696 is fixed and the minimum
                // Microsoft.Data.SqlClient version this transport depends on carries the fix.
                //
                // Until then the open is bounded by Connect Timeout rather than by the token. The
                // check above, TryPeek below, and the Task.Delay at the end of this method all still
                // observe the token, so shutdown stays prompt in the normal case.
                using (var connection = await connectionFactory.OpenNewConnection(CancellationToken.None).ConfigureAwait(false))
                {
                    messageCount = await inputQueue.TryPeek(connection, null, cancellationToken: cancellationToken).ConfigureAwait(false);

                    scope.Complete();
                }

                circuitBreaker.Success();
            }
            catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
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

        static readonly ILog Logger = LogManager.GetLogger<QueuePeeker>();
    }
}