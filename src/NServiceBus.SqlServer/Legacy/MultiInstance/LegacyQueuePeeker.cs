namespace NServiceBus.Transports.SQLServer.Legacy.MultiInstance
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class LegacyQueuePeeker : IPeekMessagesInQueue
    {
        public LegacyQueuePeeker(LegacySqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task<int> Peek(ITableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken)
        {
            var messageCount = 0;

            try
            {
                using (var connection = await connectionFactory.OpenNewConnection(inputQueue.TransportAddress).ConfigureAwait(false))
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

        LegacySqlConnectionFactory connectionFactory;

        static TimeSpan peekDelay = TimeSpan.FromSeconds(1);
        static ILog Logger = LogManager.GetLogger<LegacyQueuePeeker>();
    }
}