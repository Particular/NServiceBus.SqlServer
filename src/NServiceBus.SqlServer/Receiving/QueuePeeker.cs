namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    interface IPeekMessagesInQueue
    {
        Task<int> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken);
    }

    class QueuePeeker : IPeekMessagesInQueue
    {
        public QueuePeeker(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task<int> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken)
        {
            var messageCount = 0;

            try
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                    messageCount = await inputQueue.TryPeek(connection, cancellationToken).ConfigureAwait(false);

                    circuitBreaker.Success();

                    if (messageCount == 0)
                    {
                        await Task.Delay(peekDelay, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Warn("Sql peek operation failed", ex);
                await circuitBreaker.Failure(ex).ConfigureAwait(false);
            }

            return messageCount;
        }

        SqlConnectionFactory connectionFactory;

        static TimeSpan peekDelay = TimeSpan.FromSeconds(1);
        static ILog Logger = LogManager.GetLogger<QueuePeeker>();
    }
}