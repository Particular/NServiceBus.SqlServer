namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class DueDelayedMessageProcessor
    {
        public DueDelayedMessageProcessor(DelayedMessageTable table, SqlConnectionFactory connectionFactory, int batchSize, TimeSpan waitTimeCircuitBreaker, HostSettings hostSettings)
        {
            this.hostSettings = hostSettings;
            this.waitTimeCircuitBreaker = waitTimeCircuitBreaker;
            this.table = table;
            this.connectionFactory = connectionFactory;
            this.batchSize = batchSize;
            nextExecution = DateTimeOffset.MinValue;
            oneMinute = TimeSpan.FromMinutes(1);

            table.OnStoreDelayedMessage += OnDelayedMessageStored;
        }

        public void Start(CancellationToken cancellationToken = default)
        {
            moveDelayedMessagesCancellationTokenSource = new CancellationTokenSource();

            dueDelayedMessageProcessorCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("due delayed message processing", waitTimeCircuitBreaker, ex => hostSettings.CriticalErrorAction("Failed to move matured delayed messages to input queue", ex, moveDelayedMessagesCancellationTokenSource.Token));

            // Task.Run() so the call returns immediately instead of waiting for the first await or return down the call stack
            moveDelayedMessagesTask = Task.Run(() => MoveMaturedDelayedMessagesAndSwallowExceptions(moveDelayedMessagesCancellationTokenSource.Token), CancellationToken.None);
        }

        public async Task Stop(CancellationToken cancellationToken = default)
        {
            moveDelayedMessagesCancellationTokenSource?.Cancel();

            await moveDelayedMessagesTask.ConfigureAwait(false);

            moveDelayedMessagesCancellationTokenSource?.Dispose();
        }

        async Task MoveMaturedDelayedMessagesAndSwallowExceptions(CancellationToken moveDelayedMessagesCancellationToken)
        {
            while (!moveDelayedMessagesCancellationToken.IsCancellationRequested)
            {
                try
                {
                    try
                    {
                        var nextDueTime = await ExecuteOnce(moveDelayedMessagesCancellationToken).ConfigureAwait(false);

                        dueDelayedMessageProcessorCircuitBreaker.Success();

                        await WaitForNextExecution(nextDueTime, moveDelayedMessagesCancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!ex.IsCausedBy(moveDelayedMessagesCancellationToken))
                    {
                        Logger.Error("Exception thrown while moving matured delayed messages", ex);
                        await dueDelayedMessageProcessorCircuitBreaker.Failure(ex, moveDelayedMessagesCancellationToken).ConfigureAwait(false);
                        // since the circuit breaker might already delay a bit and we were supposed to move messages, let's try again
                        continue;
                    }
                }
                catch (Exception ex) when (ex.IsCausedBy(moveDelayedMessagesCancellationToken))
                {
                    // private token, processor is being stopped, log the exception in case the stack trace is ever needed for debugging
                    Logger.Debug("Operation canceled while stopping the moving of matured delayed messages.", ex);
                    break;
                }
            }
        }

        async Task<DateTimeOffset> ExecuteOnce(CancellationToken moveDelayedMessagesCancellationToken)
        {
            using (var connection = await connectionFactory.OpenNewConnection(moveDelayedMessagesCancellationToken).ConfigureAwait(false))
            {
                using (var transaction = connection.BeginTransaction())
                {
                    var nextDueTime = await table.MoveDueMessages(batchSize, connection, transaction, moveDelayedMessagesCancellationToken).ConfigureAwait(false);
                    transaction.Commit();
                    return nextDueTime;
                }
            }
        }

        async Task WaitForNextExecution(DateTimeOffset nextDueTime, CancellationToken moveDelayedMessagesCancellationToken)
        {
            var now = DateTimeOffset.UtcNow;

            if (nextDueTime <= now)
            {
                Logger.Debug("Scheduling next attempt to move matured delayed messages immediately because a full batch was detected.");
                return;
            }

            var timeToNext = nextDueTime - now;

            if (timeToNext <= oneMinute)
            {
                Logger.Debug($"Scheduling next attempt to move matured delayed messages for time of next message due at {nextDueTime}.");
                nextExecution = nextDueTime;
            }
            else
            {
                Logger.Debug($"Scheduling next attempt to move matured delayed messages in 1 minute.");
                nextExecution = now + oneMinute;
            }

            while (DateTimeOffset.UtcNow < nextExecution)
            {
                await Task.Delay(1000, moveDelayedMessagesCancellationToken).ConfigureAwait(false);
            }
        }
        void OnDelayedMessageStored(object sender, DateTimeOffset dueTime)
        {
            if (dueTime < nextExecution)
            {
                nextExecution = dueTime;
            }
        }

        readonly TimeSpan waitTimeCircuitBreaker;
        readonly HostSettings hostSettings;
        readonly DelayedMessageTable table;
        readonly SqlConnectionFactory connectionFactory;
        readonly int batchSize;

        CancellationTokenSource moveDelayedMessagesCancellationTokenSource;
        Task moveDelayedMessagesTask;
        RepeatedFailuresOverTimeCircuitBreaker dueDelayedMessageProcessorCircuitBreaker;
        DateTimeOffset nextExecution;
        TimeSpan oneMinute;

        static readonly ILog Logger = LogManager.GetLogger<DueDelayedMessageProcessor>();
    }
}
