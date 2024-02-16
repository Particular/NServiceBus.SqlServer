namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class DueDelayedMessageProcessor
    {
        public DueDelayedMessageProcessor(DelayedMessageTable table, DbConnectionFactory connectionFactory,
            int batchSize, TimeSpan waitTimeCircuitBreaker, HostSettings hostSettings)
        {
            this.hostSettings = hostSettings;
            this.waitTimeCircuitBreaker = waitTimeCircuitBreaker;
            this.table = table;
            this.connectionFactory = connectionFactory;
            this.batchSize = batchSize;
            nextExecution = DateTime.MinValue;

            table.OnStoreDelayedMessage += OnDelayedMessageStored;
        }

        public void Start(CancellationToken cancellationToken = default)
        {
            moveDelayedMessagesCancellationTokenSource = new CancellationTokenSource();

            dueDelayedMessageProcessorCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "due delayed message processing", waitTimeCircuitBreaker,
                ex => hostSettings.CriticalErrorAction("Failed to move matured delayed messages to input queue", ex,
                    moveDelayedMessagesCancellationTokenSource.Token));

            // Task.Run() so the call returns immediately instead of waiting for the first await or return down the call stack
            moveDelayedMessagesTask =
                Task.Run(
                    () => MoveMaturedDelayedMessagesAndSwallowExceptions(moveDelayedMessagesCancellationTokenSource
                        .Token), CancellationToken.None);
        }

        public async Task Stop(CancellationToken cancellationToken = default)
        {
            moveDelayedMessagesCancellationTokenSource?.Cancel();

            await moveDelayedMessagesTask.ConfigureAwait(false);

            moveDelayedMessagesCancellationTokenSource?.Dispose();
        }

        /// <summary>
        /// Moves messages from delayed queue to incoming queue
        /// </summary>
        async Task MoveMaturedDelayedMessagesAndSwallowExceptions(
            CancellationToken moveDelayedMessagesCancellationToken)
        {
            while (!moveDelayedMessagesCancellationToken.IsCancellationRequested)
            {
                try
                {
                    try
                    {
                        var nextDueTime = await ExecuteOnce(moveDelayedMessagesCancellationToken).ConfigureAwait(false);
                        whenToRunNext.SetNextDelayedMessage(nextDueTime);

                        dueDelayedMessageProcessorCircuitBreaker.Success();

                        await WaitForNextExecution(moveDelayedMessagesCancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!ex.IsCausedBy(moveDelayedMessagesCancellationToken))
                    {
                        Logger.Error("Exception thrown while moving matured delayed messages", ex);
                        await dueDelayedMessageProcessorCircuitBreaker.Failure(ex, moveDelayedMessagesCancellationToken)
                            .ConfigureAwait(false);
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

        async Task<DateTime> ExecuteOnce(CancellationToken moveDelayedMessagesCancellationToken)
        {
            using (var connection = await connectionFactory.OpenNewConnection(moveDelayedMessagesCancellationToken)
                       .ConfigureAwait(false))
            {
                using (var transaction = connection.BeginTransaction())
                {
                    var nextDueTime = await table
                        .MoveDueMessages(batchSize, connection, transaction, moveDelayedMessagesCancellationToken)
                        .ConfigureAwait(false);
                    transaction.Commit();

                    return nextDueTime;
                }
            }
        }

        async Task WaitForNextExecution(CancellationToken moveDelayedMessagesCancellationToken)
        {
            // Whoops, we should've already moved the delayed message! Quickly, move on! :-)
            if (whenToRunNext.AreDelayedMessagesMatured)
            {
                Logger.Debug(
                    "Scheduling next attempt to move matured delayed messages immediately because a full batch was detected.");
                return;
            }

            whenToRunNext.CalculateNextExecutionTime();
            // While running this loop, a new delayed message can be stored
            // and NextExecutionTime could be set to a new (sooner) time.
            while (DateTime.UtcNow < whenToRunNext.NextExecutionTime)
            {
                await Task.Delay(1000, moveDelayedMessagesCancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// If a delayed message is stored with a dueTime lower than nextExecution time, then reset nextExecutionTime
        /// </summary>
        void OnDelayedMessageStored(object sender, DateTime dueTime)
        {
            Logger.Debug($"Delayed messages stored which is due at {dueTime}");

            if (dueTime < nextExecution)
            {
                nextExecution = dueTime;
            }
        }

        readonly TimeSpan waitTimeCircuitBreaker;
        readonly HostSettings hostSettings;
        readonly DelayedMessageTable table;
        readonly DbConnectionFactory connectionFactory;
        readonly int batchSize;
        // WhenToRunNext whenToRunNext = new();

        CancellationTokenSource moveDelayedMessagesCancellationTokenSource;
        Task moveDelayedMessagesTask;
        RepeatedFailuresOverTimeCircuitBreaker dueDelayedMessageProcessorCircuitBreaker;
        DateTime nextExecution;

        static readonly ILog Logger = LogManager.GetLogger<DueDelayedMessageProcessor>();
        WhenToRunNext whenToRunNext = new();

        class WhenToRunNext
        {
            public void SetNextDelayedMessage(DateTime dueDate)
            {
                NextDelayedMessage = dueDate;
                if (dueDate == DateTime.MinValue)
                {
                    Logger.Debug("No delayed messages available...");
                    DelayedMessageAvailable = false;
                    return;
                }

                Logger.Debug($"Delayed message found with due date of {dueDate}");
                DelayedMessageAvailable = true;
                NextExecutionTime = dueDate;
            }

            public void CalculateNextExecutionTime()
            {
                IncreaseExponentialBackOff();

                var newExecutionTime = DateTime.UtcNow.AddMilliseconds(milliseconds);

                // If the next delayed message is coming up before the back-off time, use that.
                if (DelayedMessageAvailable && newExecutionTime > NextDelayedMessage)
                {
                    Logger.Debug(
                        $"Scheduling next attempt to move matured delayed messages for time of next message due at {NextDelayedMessage}.");
                    NextExecutionTime = NextDelayedMessage;
                    // We find a better time to execute, so we can reset the exponential back off
                    milliseconds = 500;
                    return;
                }

                Logger.Debug(
                    $"Exponentially backing off for {milliseconds / 1000} seconds until {newExecutionTime}.");
                NextExecutionTime = newExecutionTime;
            }

            void IncreaseExponentialBackOff()
            {
                milliseconds *= 2;
                if (milliseconds > 60000)
                {
                    milliseconds = 60000;
                }
            }

            public bool AreDelayedMessagesMatured =>
                DelayedMessageAvailable && NextExecutionTime < DateTime.UtcNow;

            DateTime NextDelayedMessage { get; set; } = DateTime.UtcNow;
            public DateTime NextExecutionTime { get; private set; } = DateTime.UtcNow.AddSeconds(2);
            bool DelayedMessageAvailable { get; set; }

            int milliseconds = 500; // First time multiplied will be 1 second.
        }
    }
}