namespace NServiceBus.Transport.SqlServer
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class DueDelayedMessageProcessor
    {
        public DueDelayedMessageProcessor(DelayedMessageTable table, SqlConnectionFactory connectionFactory, TimeSpan interval, int batchSize, TimeSpan waitTimeCircuitBreaker, HostSettings hostSettings)
        {
            this.hostSettings = hostSettings;
            this.waitTimeCircuitBreaker = waitTimeCircuitBreaker;
            this.table = table;
            this.connectionFactory = connectionFactory;
            this.interval = interval;
            this.batchSize = batchSize;
            message = $"Scheduling next attempt to move matured delayed messages to input queue in {interval}";
        }

        public void Start(CancellationToken cancellationToken = default)
        {
            moveDelayedMessagesCancellationTokenSource = new CancellationTokenSource();

            dueDelayedMessageProcessorCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("due delayed message processing", waitTimeCircuitBreaker, ex => hostSettings.CriticalErrorAction("Failed to move matured delayed messages to input queue", ex, moveDelayedMessagesCancellationTokenSource.Token));

            moveDelayedMessagesTask = Task.Run(() => MoveMaturedDelayedMessages(moveDelayedMessagesCancellationTokenSource.Token), CancellationToken.None);
        }

        public async Task Stop(CancellationToken cancellationToken = default)
        {
            moveDelayedMessagesCancellationTokenSource?.Cancel();

            await moveDelayedMessagesTask.ConfigureAwait(false);

            moveDelayedMessagesCancellationTokenSource?.Dispose();
        }

        async Task MoveMaturedDelayedMessages(CancellationToken moveDelayedMessagesCancellationToken)
        {
            while (!moveDelayedMessagesCancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (var connection = await connectionFactory.OpenNewConnection(moveDelayedMessagesCancellationToken).ConfigureAwait(false))
                    {
                        using (var transaction = connection.BeginTransaction())
                        {
                            await table.MoveDueMessages(batchSize, connection, transaction, moveDelayedMessagesCancellationToken).ConfigureAwait(false);
                            transaction.Commit();
                        }
                    }

                    dueDelayedMessageProcessorCircuitBreaker.Success();
                }
                catch (OperationCanceledException ex)
                {
                    // Graceful shutdown
                    if (moveDelayedMessagesCancellationToken.IsCancellationRequested)
                    {
                        Logger.Debug("Delayed message poller cancelled.", ex);
                    }
                    else
                    {
                        Logger.Warn("OperationCanceledException thrown.", ex);
                    }
                    return;
                }
                catch (SqlException e) when (moveDelayedMessagesCancellationToken.IsCancellationRequested)
                {
                    Logger.Debug("Exception thrown while performing cancellation", e);
                    return;
                }
                catch (Exception e)
                {
                    Logger.Error("Exception thrown while moving matured delayed messages", e);
                    await dueDelayedMessageProcessorCircuitBreaker.Failure(e, moveDelayedMessagesCancellationToken).ConfigureAwait(false);
                    // since the circuit breaker might already delay a bit and we were supposed to move messages let's try again.
                    continue;
                }

                try
                {
                    Logger.Debug(message);
                    await Task.Delay(interval, moveDelayedMessagesCancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex)
                {
                    if (moveDelayedMessagesCancellationToken.IsCancellationRequested)
                    {
                        Logger.Debug("Delayed message poller cancelled.", ex);
                    }
                    else
                    {
                        Logger.Warn("OperationCanceledException thrown.", ex);
                    }
                    return;
                }
            }
        }

        readonly TimeSpan waitTimeCircuitBreaker;
        readonly HostSettings hostSettings;
        readonly string message;
        readonly DelayedMessageTable table;
        readonly SqlConnectionFactory connectionFactory;
        readonly TimeSpan interval;
        readonly int batchSize;

        CancellationTokenSource moveDelayedMessagesCancellationTokenSource;
        Task moveDelayedMessagesTask;
        RepeatedFailuresOverTimeCircuitBreaker dueDelayedMessageProcessorCircuitBreaker;

        static ILog Logger = LogManager.GetLogger<DueDelayedMessageProcessor>();
    }
}