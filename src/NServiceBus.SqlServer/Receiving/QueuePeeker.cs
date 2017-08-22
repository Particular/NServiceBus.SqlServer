namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
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

        public async Task<int> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken)
        {
            var messageCount = 0;

            try
            {
#if NET452
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
#else
                using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
#endif
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                    messageCount = await inputQueue.TryPeek(connection, cancellationToken).ConfigureAwait(false);

                    circuitBreaker.Success();

                    if (messageCount == 0)
                    {
                        Logger.Debug($"Input queue empty. Next peek operation will be delayed for {settings.Delay}.");

                        await Task.Delay(settings.Delay, cancellationToken).ConfigureAwait(false);
                    }

                    scope.Complete();
                }
            }
            catch (OperationCanceledException)
            {
                //Graceful shutdown
            }
            catch (SqlException e) when (cancellationToken.IsCancellationRequested)
            {
                Logger.Debug("Exception thown while performing cancellation", e);
            }
            catch (Exception ex)
            {
                Logger.Warn("Sql peek operation failed", ex);
                await circuitBreaker.Failure(ex).ConfigureAwait(false);
            }

            return messageCount;
        }

        SqlConnectionFactory connectionFactory;
        QueuePeekerOptions settings;

        static ILog Logger = LogManager.GetLogger<QueuePeeker>();
    }
}