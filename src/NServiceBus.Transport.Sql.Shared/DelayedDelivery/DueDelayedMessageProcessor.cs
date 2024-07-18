namespace NServiceBus.Transport.Sql.Shared.PubSub
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Configuration;
    using Logging;
    using Receiving;
    using DelayedDelivery;

    class DueDelayedMessageProcessor
    {
        public DueDelayedMessageProcessor(DelayedMessageTable table, DbConnectionFactory connectionFactory, IExceptionClassifier exceptionClassifier,
            int batchSize, TimeSpan waitTimeCircuitBreaker, HostSettings hostSettings)
        {
            this.hostSettings = hostSettings;
            this.waitTimeCircuitBreaker = waitTimeCircuitBreaker;
            this.table = table;
            this.connectionFactory = connectionFactory;
            this.exceptionClassifier = exceptionClassifier;
            this.batchSize = batchSize;

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
                        backOffStrategy.RegisterNewDueTime(nextDueTime);

                        dueDelayedMessageProcessorCircuitBreaker.Success();

                        await backOffStrategy.WaitForNextExecution(moveDelayedMessagesCancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, moveDelayedMessagesCancellationToken))
                    {
                        Logger.Error("Exception thrown while moving matured delayed messages", ex);
                        await dueDelayedMessageProcessorCircuitBreaker.Failure(ex, moveDelayedMessagesCancellationToken)
                            .ConfigureAwait(false);
                        // since the circuit breaker might already delay a bit and we were supposed to move messages, let's try again
                        continue;
                    }
                }
                catch (Exception ex) when (exceptionClassifier.IsOperationCancelled(ex, moveDelayedMessagesCancellationToken))
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

        /// <summary>
        /// If a delayed message is stored with a dueTime lower than nextExecution time, then reset nextExecutionTime
        /// </summary>
        void OnDelayedMessageStored(object sender, DateTime dueTime)
        {
            Logger.Debug($"Delayed messages stored which is due at {dueTime}");
            backOffStrategy.RegisterNewDueTime(dueTime);
        }

        readonly TimeSpan waitTimeCircuitBreaker;
        readonly HostSettings hostSettings;
        readonly DelayedMessageTable table;
        readonly DbConnectionFactory connectionFactory;
        readonly IExceptionClassifier exceptionClassifier;

        readonly int batchSize;
        // WhenToRunNext whenToRunNext = new();

        CancellationTokenSource moveDelayedMessagesCancellationTokenSource;
        Task moveDelayedMessagesTask;
        RepeatedFailuresOverTimeCircuitBreaker dueDelayedMessageProcessorCircuitBreaker;

        static readonly ILog Logger = LogManager.GetLogger<DueDelayedMessageProcessor>();
        BackOffStrategy backOffStrategy = new();
    }
}