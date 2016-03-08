namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading.Tasks;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using Logging;

    class MessagePump : IPushMessages
    {
        public MessagePump(Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory, IPurgeQueues queuePurger, IPurgeExpiredMessages expiredMessagesPurger, IPeekMessagesInQueue queuePeeker, QueueAddressParser addressParser, TimeSpan waitTimeCircuitBreaker)
        {
            this.receiveStrategyFactory = receiveStrategyFactory;
            this.queuePurger = queuePurger;
            this.expiredMessagesPurger = expiredMessagesPurger;
            this.queuePeeker = queuePeeker;
            this.addressParser = addressParser;
            this.waitTimeCircuitBreaker = waitTimeCircuitBreaker;
        }

        public async Task Init(Func<PushContext, Task> pipe, CriticalError criticalError, PushSettings settings)
        {
            pipeline = pipe;

            receiveStrategy = receiveStrategyFactory(settings.RequiredTransactionMode);

            peekCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("SqlPeek", waitTimeCircuitBreaker, ex => criticalError.Raise("Failed to peek " + settings.InputQueue, ex));
            receiveCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("ReceiveText", waitTimeCircuitBreaker, ex => criticalError.Raise("Failed to receive from " + settings.InputQueue, ex));

            inputQueue = new TableBasedQueue(addressParser.Parse(settings.InputQueue));
            errorQueue = new TableBasedQueue(addressParser.Parse(settings.ErrorQueue));

            if (settings.PurgeOnStartup)
            {
                var purgedRowsCount = await queuePurger.Purge(inputQueue).ConfigureAwait(false);

                Logger.InfoFormat("{0} messages was purged from table {1}", purgedRowsCount, settings.InputQueue);
            }
        }

        public void Start(PushRuntimeSettings limitations)
        {
            runningReceiveTasks = new ConcurrentDictionary<Task, Task>();
            concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency);
            cancellationTokenSource = new CancellationTokenSource();

            cancellationToken = cancellationTokenSource.Token;

            messagePumpTask = Task.Run(ProcessMessages, CancellationToken.None);
            purgeTask = Task.Run(PurgeExpiredMessages, CancellationToken.None);
        }

        public async Task Stop()
        {
            cancellationTokenSource.Cancel();

            // ReSharper disable once MethodSupportsCancellation
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var allTasks = runningReceiveTasks.Values.Concat(new[]
            {
                messagePumpTask,
                purgeTask
            });
            var finishedTask = await Task.WhenAny(Task.WhenAll(allTasks), timeoutTask).ConfigureAwait(false);

            if (finishedTask.Equals(timeoutTask))
            {
                Logger.Error("The message pump failed to stop with in the time allowed(30s)");
            }

            concurrencyLimiter.Dispose();
            cancellationTokenSource.Dispose();

            runningReceiveTasks.Clear();
        }

        async Task ProcessMessages()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InnerProcessMessages().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // For graceful shutdown purposes
                }
                catch (Exception ex)
                {
                    Logger.Error("Sql Message pump failed", ex);
                    await peekCircuitBreaker.Failure(ex).ConfigureAwait(false);
                }
            }
        }

        async Task InnerProcessMessages()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var messageCount = await queuePeeker.Peek(inputQueue, peekCircuitBreaker, cancellationToken).ConfigureAwait(false);

                if (messageCount == 0)
                {
                    continue;
                }               

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                for (var i = 0; i < messageCount; i++)
                {
                    await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                    var tokenSource = new CancellationTokenSource();

                    var receiveTask = Task.Run(async () =>
                    {
                        try
                        {
                            await receiveStrategy.ReceiveMessage(inputQueue, errorQueue, tokenSource, pipeline)
                                .ConfigureAwait(false);

                            receiveCircuitBreaker.Success();
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("Sql receive operation failed", ex);
                            await receiveCircuitBreaker.Failure(ex).ConfigureAwait(false);
                        }
                        finally
                        {
                            concurrencyLimiter.Release();
                        }
                    }, cancellationToken).ContinueWith(t => tokenSource.Dispose());

                    runningReceiveTasks.TryAdd(receiveTask, receiveTask);

                    // We insert the original task into the runningReceiveTasks because we want to await the completion
                    // of the running receives. ExecuteSynchronously is a request to execute the continuation as part of
                    // the transition of the antecedents completion phase. This means in most of the cases the continuation
                    // will be executed during this transition and the antecedent task goes into the completion state only 
                    // after the continuation is executed. This is not always the case. When the TPL thread handling the
                    // antecedent task is aborted the continuation will be scheduled. But in this case we don't need to await
                    // the continuation to complete because only really care about the receive operations. The final operation
                    // when shutting down is a clear of the running tasks anyway.
                    receiveTask.ContinueWith(t =>
                    {
                        Task toBeRemoved;
                        runningReceiveTasks.TryRemove(t, out toBeRemoved);
                    }, TaskContinuationOptions.ExecuteSynchronously)
                    .Ignore();
                }
            }
        }

        async Task PurgeExpiredMessages()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await expiredMessagesPurger.Purge(inputQueue, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                }
            }
        }

        TableBasedQueue inputQueue;
        TableBasedQueue errorQueue;
        Func<PushContext, Task> pipeline;
        Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory;
        IPurgeQueues queuePurger;
        IPurgeExpiredMessages expiredMessagesPurger;
        IPeekMessagesInQueue queuePeeker;
        QueueAddressParser addressParser;
        TimeSpan waitTimeCircuitBreaker;
        ConcurrentDictionary<Task, Task> runningReceiveTasks;
        SemaphoreSlim concurrencyLimiter;
        CancellationTokenSource cancellationTokenSource;
        CancellationToken cancellationToken;
        RepeatedFailuresOverTimeCircuitBreaker peekCircuitBreaker;
        RepeatedFailuresOverTimeCircuitBreaker receiveCircuitBreaker;

        static ILog Logger = LogManager.GetLogger<MessagePump>();
        Task messagePumpTask;
        Task purgeTask;
        ReceiveStrategy receiveStrategy;
    }
}