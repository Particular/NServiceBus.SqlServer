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

    class MessagePump : IMessageReceiver
    {
        public MessagePump(SqlServerTransport transport, ReceiveSettings receiveSettings, HostSettings hostSettings, Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory, Func<string, TableBasedQueue> queueFactory, IPurgeQueues queuePurger, IExpiredMessagesPurger expiredMessagesPurger, IPeekMessagesInQueue queuePeeker, QueuePeekerOptions queuePeekerOptions, SchemaInspector schemaInspector, TimeSpan waitTimeCircuitBreaker, ISubscriptionManager subscriptionManager)
        {
            this.transport = transport;
            this.receiveSettings = receiveSettings;
            this.hostSettings = hostSettings;
            this.receiveStrategyFactory = receiveStrategyFactory;
            this.queuePurger = queuePurger;
            this.queueFactory = queueFactory;
            this.expiredMessagesPurger = expiredMessagesPurger;
            this.queuePeeker = queuePeeker;
            this.queuePeekerOptions = queuePeekerOptions;
            this.schemaInspector = schemaInspector;
            this.waitTimeCircuitBreaker = waitTimeCircuitBreaker;
            Subscriptions = subscriptionManager;
        }

        public async Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError, CancellationToken cancellationToken = default)
        {
            this.limitations = limitations;
            messagePumpCancellationTokenSource = new CancellationTokenSource();
            messageProcessingCancellationTokenSource = new CancellationTokenSource();

            receiveStrategy = receiveStrategyFactory(transport.TransportTransactionMode);

            peekCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("SqlPeek", waitTimeCircuitBreaker, ex => hostSettings.CriticalErrorAction("Failed to peek " + receiveSettings.ReceiveAddress, ex, messageProcessingCancellationTokenSource.Token));
            receiveCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("ReceiveText", waitTimeCircuitBreaker, ex => hostSettings.CriticalErrorAction("Failed to receive from " + receiveSettings.ReceiveAddress, ex, messageProcessingCancellationTokenSource.Token));

            inputQueue = queueFactory(receiveSettings.ReceiveAddress);
            errorQueue = queueFactory(receiveSettings.ErrorQueue);

            receiveStrategy.Init(inputQueue, errorQueue, onMessage, onError, hostSettings.CriticalErrorAction);

            if (transport.ExpiredMessagesPurger.PurgeOnStartup)
            {
                try
                {
                    var purgedRowsCount = await queuePurger.Purge(inputQueue, cancellationToken).ConfigureAwait(false);

                    Logger.InfoFormat("{0:N0} messages purged from queue {1}", purgedRowsCount, receiveSettings.ReceiveAddress);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to purge input queue on startup.", ex);
                }
            }

            await PurgeExpiredMessages(cancellationToken).ConfigureAwait(false);

            await schemaInspector.PerformInspection(inputQueue, cancellationToken).ConfigureAwait(false);
        }

        public Task StartReceive(CancellationToken cancellationToken)
        {
            inputQueue.FormatPeekCommand(queuePeekerOptions.MaxRecordsToPeek ?? Math.Min(100, 10 * limitations.MaxConcurrency));
            maxConcurrency = limitations.MaxConcurrency;
            concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency);

            messagePumpTask = Task.Run(() => ProcessMessages(messagePumpCancellationTokenSource.Token), cancellationToken);

            return Task.CompletedTask;
        }

        public async Task StopReceive(CancellationToken cancellationToken = default)
        {
            const int timeoutDurationInSeconds = 30;
            var timedCancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutDurationInSeconds));

            messagePumpCancellationTokenSource?.Cancel();

            var userOrTimedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timedCancellationSource.Token);
            var userOrTimedCancellationToken = userOrTimedCancellationTokenSource.Token;
            userOrTimedCancellationToken.Register(() => messageProcessingCancellationTokenSource?.Cancel());

            await messagePumpTask.ConfigureAwait(false);

            while (concurrencyLimiter.CurrentCount != maxConcurrency)
            {
                // Pass CancellationToken.None so that no exceptions will be thrown while waiting
                // for the message pump to gracefully shut down. The cancellation tokens passed to
                // ProcessMessages (and thus the message processing pipelines) will be responsible
                // for more forcefully shutting down message processing after the user's shutdown SLA
                // is reached
                await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
            }

            concurrencyLimiter.Dispose();
            userOrTimedCancellationTokenSource?.Dispose();
            messagePumpCancellationTokenSource?.Dispose();
            messageProcessingCancellationTokenSource?.Dispose();
        }

        async Task ProcessMessages(CancellationToken messagePumpCancellationToken)
        {
            while (!messagePumpCancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InnerProcessMessages(messagePumpCancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // For graceful shutdown purposes
                }
                catch (SqlException e) when (messagePumpCancellationToken.IsCancellationRequested)
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

        async Task InnerProcessMessages(CancellationToken messagePumpCancellationToken)
        {
            while (!messagePumpCancellationToken.IsCancellationRequested)
            {
                var messageCount = await queuePeeker.Peek(inputQueue, peekCircuitBreaker, messagePumpCancellationToken).ConfigureAwait(false);

                if (messageCount == 0)
                {
                    continue;
                }

                if (messagePumpCancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // We cannot dispose this token source because of potential race conditions of concurrent receives
                var stopBatchCancellationSource = new CancellationTokenSource();

                // If the receive or peek circuit breaker is triggered start only one message processing task at a time.
                var maximumConcurrentReceives = receiveCircuitBreaker.Triggered || peekCircuitBreaker.Triggered ? 1 : messageCount;

                for (var i = 0; i < maximumConcurrentReceives; i++)
                {
                    if (messagePumpCancellationToken.IsCancellationRequested || stopBatchCancellationSource.IsCancellationRequested)
                    {
                        break;
                    }

                    await concurrencyLimiter.WaitAsync(messagePumpCancellationToken).ConfigureAwait(false);

                    _ = InnerReceive(stopBatchCancellationSource, messageProcessingCancellationTokenSource.Token);
                }
            }
        }

        async Task InnerReceive(CancellationTokenSource stopBatch, CancellationToken messageProcessingCancellationToken)
        {
            try
            {
                // We need to force the method to continue asynchronously because SqlConnection
                // in combination with TransactionScope will apply connection pooling and enlistment synchronous in ctor.
                await Task.Yield();

                await receiveStrategy.ReceiveMessage(stopBatch, messageProcessingCancellationToken)
                    .ConfigureAwait(false);

                receiveCircuitBreaker.Success();
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
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

        async Task PurgeExpiredMessages(CancellationToken cancellationToken = default)
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
        readonly SqlServerTransport transport;
        readonly ReceiveSettings receiveSettings;
        readonly HostSettings hostSettings;
        Func<TransportTransactionMode, ReceiveStrategy> receiveStrategyFactory;
        IPurgeQueues queuePurger;
        Func<string, TableBasedQueue> queueFactory;
        IExpiredMessagesPurger expiredMessagesPurger;
        IPeekMessagesInQueue queuePeeker;
        QueuePeekerOptions queuePeekerOptions;
        SchemaInspector schemaInspector;
        TimeSpan waitTimeCircuitBreaker;
        SemaphoreSlim concurrencyLimiter;
        CancellationTokenSource messagePumpCancellationTokenSource;
        CancellationTokenSource messageProcessingCancellationTokenSource;
        int maxConcurrency;
        RepeatedFailuresOverTimeCircuitBreaker peekCircuitBreaker;
        RepeatedFailuresOverTimeCircuitBreaker receiveCircuitBreaker;
        Task messagePumpTask;
        ReceiveStrategy receiveStrategy;

        static ILog Logger = LogManager.GetLogger<MessagePump>();
        PushRuntimeSettings limitations;


        public ISubscriptionManager Subscriptions { get; }
        public string Id => receiveSettings.Id;
    }
}
