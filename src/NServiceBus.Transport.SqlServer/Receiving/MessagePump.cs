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

    class MessagePump : IPushMessages
    {
        public MessagePump(Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory, Func<string, TableBasedQueue> queueFactory, IPurgeQueues queuePurger, IExpiredMessagesPurger expiredMessagesPurger, IPeekMessagesInQueue queuePeeker, SchemaInspector schemaInspector, TimeSpan waitTimeCircuitBreaker)
        {
            this.receiveStrategyFactory = receiveStrategyFactory;
            this.queuePurger = queuePurger;
            this.queueFactory = queueFactory;
            this.expiredMessagesPurger = expiredMessagesPurger;
            this.queuePeeker = queuePeeker;
            this.schemaInspector = schemaInspector;
            this.waitTimeCircuitBreaker = waitTimeCircuitBreaker;
        }

        public async Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            receiveStrategy = receiveStrategyFactory(settings.RequiredTransactionMode);

            peekCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("SqlPeek", waitTimeCircuitBreaker, ex => criticalError.Raise("Failed to peek " + settings.InputQueue, ex));
            receiveCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("ReceiveText", waitTimeCircuitBreaker, ex => criticalError.Raise("Failed to receive from " + settings.InputQueue, ex));

            inputQueue = queueFactory(settings.InputQueue);
            errorQueue = queueFactory(settings.ErrorQueue);

            receiveStrategy.Init(inputQueue, errorQueue, onMessage, onError, criticalError);

            if (settings.PurgeOnStartup)
            {
                try
                {
                    var purgedRowsCount = await queuePurger.Purge(inputQueue).ConfigureAwait(false);

                    Logger.InfoFormat("{0:N0} messages purged from queue {1}", purgedRowsCount, settings.InputQueue);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to purge input queue on startup.", ex);
                }
            }

            await PurgeExpiredMessages().ConfigureAwait(false);

            await schemaInspector.PerformInspection(inputQueue).ConfigureAwait(false);
        }

        public void Start(PushRuntimeSettings limitations)
        {
            inputQueue.FormatPeekCommand(Math.Min(100, 10 * limitations.MaxConcurrency));
            maxConcurrency = limitations.MaxConcurrency;
            concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency);
            cancellationTokenSource = new CancellationTokenSource();

            cancellationToken = cancellationTokenSource.Token;

            messagePumpTask = Task.Run(ProcessMessages, CancellationToken.None);
        }

        public async Task Stop()
        {
            const int timeoutDurationInSeconds = 30;
            cancellationTokenSource.Cancel();

            await messagePumpTask.ConfigureAwait(false);

            try
            {
                using (var shutdownCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutDurationInSeconds)))
                {
                    while (concurrencyLimiter.CurrentCount != maxConcurrency)
                    {
                        await Task.Delay(50, shutdownCancellationTokenSource.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.ErrorFormat("The message pump failed to stop within the time allowed ({0}s)", timeoutDurationInSeconds);
            }

            concurrencyLimiter.Dispose();
            cancellationTokenSource.Dispose();
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
                catch (SqlException e) when (cancellationToken.IsCancellationRequested)
                {
                    Logger.Debug("Exception thrown during cancellation", e);
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

                // We cannot dispose this token source because of potential race conditions of concurrent receives
                var loopCancellationTokenSource = new CancellationTokenSource();

                // If the receive or peek circuit breaker is triggered start only one message processing task at a time.
                var maximumConcurrentReceives = receiveCircuitBreaker.Triggered || peekCircuitBreaker.Triggered ? 1 : messageCount;

                for (var i = 0; i < maximumConcurrentReceives; i++)
                {
                    if (loopCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                    _ = InnerReceive(loopCancellationTokenSource);
                }
            }
        }

        async Task InnerReceive(CancellationTokenSource loopCancellationTokenSource)
        {
            try
            {
                // We need to force the method to continue asynchronously because SqlConnection
                // in combination with TransactionScope will apply connection pooling and enlistment synchronous in ctor.
                await Task.Yield();

                await receiveStrategy.ReceiveMessage(loopCancellationTokenSource)
                    .ConfigureAwait(false);

                receiveCircuitBreaker.Success();
            }
            catch (SqlException e) when (e.Number == 1205)
            {
                //Receive has been victim of a lock resolution
                Logger.Warn("Sql receive operation failed.", e);
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
        }

        async Task PurgeExpiredMessages()
        {
            try
            {
                await expiredMessagesPurger.Purge(inputQueue, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (SqlException e) when (e.Number == 1205)
            {
                //Purge has been victim of a lock resolution
                Logger.Warn("Purger has been selected as a lock victim.", e);
            }
            catch (SqlException e) when (cancellationToken.IsCancellationRequested)
            {
                Logger.Debug("Exception thrown while performing cancellation", e);
            }
        }

        TableBasedQueue inputQueue;
        TableBasedQueue errorQueue;
        Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory;
        IPurgeQueues queuePurger;
        Func<string, TableBasedQueue> queueFactory;
        IExpiredMessagesPurger expiredMessagesPurger;
        IPeekMessagesInQueue queuePeeker;
        SchemaInspector schemaInspector;
        TimeSpan waitTimeCircuitBreaker;
        SemaphoreSlim concurrencyLimiter;
        CancellationTokenSource cancellationTokenSource;
        CancellationToken cancellationToken;
        int maxConcurrency;
        RepeatedFailuresOverTimeCircuitBreaker peekCircuitBreaker;
        RepeatedFailuresOverTimeCircuitBreaker receiveCircuitBreaker;
        Task messagePumpTask;
        ReceiveStrategy receiveStrategy;

        static ILog Logger = LogManager.GetLogger<MessagePump>();
    }
}
