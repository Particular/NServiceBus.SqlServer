namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
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
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                    messageCount = await inputQueue.TryPeek(connection, cancellationToken).ConfigureAwait(false);

                    circuitBreaker.Success();

                    if (messageCount == 0)
                    {
                        Logger.Debug($"Input queue empty. Next peek operation will be delayed for {settings.Delay}.");

                        await Task.Delay(settings.Delay, cancellationToken).ConfigureAwait(false);
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

        readonly SqlConnectionFactory connectionFactory;
        readonly QueuePeekerOptions settings;

        static ILog Logger = LogManager.GetLogger<QueuePeeker>();
    }
}