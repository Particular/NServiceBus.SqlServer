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

    class MessageReceiver : IMessageReceiver
    {
        public MessageReceiver(SqlServerTransport transport, ReceiveSettings receiveSettings, HostSettings hostSettings, Func<TransportTransactionMode, ProcessStrategy> processStrategyFactory, Func<string, TableBasedQueue> queueFactory, IPurgeQueues queuePurger, IExpiredMessagesPurger expiredMessagesPurger, IPeekMessagesInQueue queuePeeker, QueuePeekerOptions queuePeekerOptions, SchemaInspector schemaInspector, TimeSpan waitTimeCircuitBreaker, ISubscriptionManager subscriptionManager)
        {
            this.transport = transport;
            this.receiveSettings = receiveSettings;
            this.hostSettings = hostSettings;
            this.processStrategyFactory = processStrategyFactory;
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
            messageReceivingCancellationTokenSource = new CancellationTokenSource();
            messageProcessingCancellationTokenSource = new CancellationTokenSource();

            processStrategy = processStrategyFactory(transport.TransportTransactionMode);

            messageReceivingCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("message receiving", waitTimeCircuitBreaker, ex => hostSettings.CriticalErrorAction("Failed to peek " + receiveSettings.ReceiveAddress, ex, messageProcessingCancellationTokenSource.Token));
            messageProcessingCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("message processing", waitTimeCircuitBreaker, ex => hostSettings.CriticalErrorAction("Failed to receive from " + receiveSettings.ReceiveAddress, ex, messageProcessingCancellationTokenSource.Token));

            inputQueue = queueFactory(receiveSettings.ReceiveAddress);
            errorQueue = queueFactory(receiveSettings.ErrorQueue);

            processStrategy.Init(inputQueue, errorQueue, onMessage, onError, hostSettings.CriticalErrorAction);

            if (transport.ExpiredMessagesPurger.PurgeOnStartup)
            {
                try
                {
                    var purgedRowsCount = await queuePurger.Purge(inputQueue, cancellationToken).ConfigureAwait(false);

                    Logger.InfoFormat("{0:N0} messages purged from queue {1}", purgedRowsCount, receiveSettings.ReceiveAddress);
                }
                catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
                {
                    Logger.Warn("Failed to purge input queue on startup.", ex);
                }
            }

            await PurgeExpiredMessages(cancellationToken).ConfigureAwait(false);

            await schemaInspector.PerformInspection(inputQueue, cancellationToken).ConfigureAwait(false);
        }

        public Task StartReceive(CancellationToken cancellationToken = default)
        {
            inputQueue.FormatPeekCommand(queuePeekerOptions.MaxRecordsToPeek ?? Math.Min(100, 10 * limitations.MaxConcurrency));
            maxConcurrency = limitations.MaxConcurrency;
            concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency);

            // Task.Run() so the call returns immediately instead of waiting for the first await or return down the call stack
            messageReceivingTask = Task.Run(() => ReceiveMessagesAndSwallowExceptions(messageReceivingCancellationTokenSource.Token), CancellationToken.None);

            return Task.CompletedTask;
        }

        public async Task StopReceive(CancellationToken cancellationToken = default)
        {
            messageReceivingCancellationTokenSource?.Cancel();

            using (cancellationToken.Register(() => messageProcessingCancellationTokenSource?.Cancel()))
            {
                await messageReceivingTask.ConfigureAwait(false);

                while (concurrencyLimiter.CurrentCount != maxConcurrency)
                {
                    // Pass CancellationToken.None so that no exceptions will be thrown while waiting
                    // for the message receiver to gracefully shut down. The cancellation tokens passed to
                    // ProcessMessages (and thus the message processing pipelines) will be responsible
                    // for more forcefully shutting down message processing after the user's shutdown SLA
                    // is reached
                    await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
                }
            }

            messageReceivingCircuitBreaker.Dispose();
            messageProcessingCircuitBreaker.Dispose();
            concurrencyLimiter.Dispose();
            messageReceivingCancellationTokenSource?.Dispose();
            messageProcessingCancellationTokenSource?.Dispose();
        }

        async Task ReceiveMessagesAndSwallowExceptions(CancellationToken messageReceivingCancellationToken)
        {
            while (!messageReceivingCancellationToken.IsCancellationRequested)
            {
                try
                {
                    try
                    {
                        await ReceiveMessages(messageReceivingCancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!ex.IsCausedBy(messageReceivingCancellationToken))
                    {
                        Logger.Error("Message receiving failed", ex);
                        await messageReceivingCircuitBreaker.Failure(ex, messageReceivingCancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex.IsCausedBy(messageReceivingCancellationToken))
                {
                    // private token, receiver is being stopped, log the exception in case the stack trace is ever needed for debugging
                    Logger.Debug("Operation canceled while stopping the message receiver.", ex);
                    break;
                }
            }
        }

        async Task ReceiveMessages(CancellationToken messageReceivingCancellationToken)
        {
            var messageCount = await queuePeeker.Peek(inputQueue, messageReceivingCircuitBreaker, messageReceivingCancellationToken).ConfigureAwait(false);

            if (messageCount == 0)
            {
                return;
            }

            messageReceivingCancellationToken.ThrowIfCancellationRequested();

            // We cannot dispose this token source because of potential race conditions of concurrent processing
            var stopBatchCancellationSource = new CancellationTokenSource();

            // If either the receiving or processing circuit breakers are triggered, start only one message processing task at a time.
            var maximumConcurrentProcessing = messageProcessingCircuitBreaker.Triggered || messageReceivingCircuitBreaker.Triggered ? 1 : messageCount;

            for (var i = 0; i < maximumConcurrentProcessing; i++)
            {
                if (stopBatchCancellationSource.IsCancellationRequested)
                {
                    break;
                }

                await concurrencyLimiter.WaitAsync(messageReceivingCancellationToken).ConfigureAwait(false);

                _ = ProcessMessagesSwallowExceptionsAndReleaseConcurrencyLimiter(stopBatchCancellationSource, messageProcessingCancellationTokenSource.Token);
            }
        }

        async Task ProcessMessagesSwallowExceptionsAndReleaseConcurrencyLimiter(CancellationTokenSource stopBatchCancellationTokenSource, CancellationToken messageProcessingCancellationToken)
        {
            try
            {
                try
                {
                    // We need to force the method to continue asynchronously because SqlConnection
                    // in combination with TransactionScope will apply connection pooling and enlistment synchronous in ctor.
                    await Task.Yield();

                    await processStrategy.ProcessMessage(stopBatchCancellationTokenSource, messageProcessingCancellationToken)
                        .ConfigureAwait(false);

                    messageProcessingCircuitBreaker.Success();
                }
                catch (SqlException ex) when (ex.Number == 1205)
                {
                    // getting the message was the victim of a lock resolution
                    Logger.Warn("Message processing failed", ex);
                }
                catch (Exception ex) when (!ex.IsCausedBy(messageProcessingCancellationToken))
                {
                    Logger.Warn("Message processing failed", ex);

                    await messageProcessingCircuitBreaker.Failure(ex, messageProcessingCancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex.IsCausedBy(messageProcessingCancellationToken))
            {
                Logger.Debug("Message processing canceled.", ex);
            }
            finally
            {
                concurrencyLimiter.Release();
            }
        }

        async Task PurgeExpiredMessages(CancellationToken cancellationToken)
        {
            try
            {
                await expiredMessagesPurger.Purge(inputQueue, cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException e) when (e.Number == 1205)
            {
                //Purge has been victim of a lock resolution
                Logger.Warn("Purger has been selected as a lock victim.", e);
            }
        }

        TableBasedQueue inputQueue;
        TableBasedQueue errorQueue;
        readonly SqlServerTransport transport;
        readonly ReceiveSettings receiveSettings;
        readonly HostSettings hostSettings;
        readonly Func<TransportTransactionMode, ProcessStrategy> processStrategyFactory;
        readonly IPurgeQueues queuePurger;
        readonly Func<string, TableBasedQueue> queueFactory;
        readonly IExpiredMessagesPurger expiredMessagesPurger;
        readonly IPeekMessagesInQueue queuePeeker;
        readonly QueuePeekerOptions queuePeekerOptions;
        readonly SchemaInspector schemaInspector;
        TimeSpan waitTimeCircuitBreaker;
        SemaphoreSlim concurrencyLimiter;
        CancellationTokenSource messageReceivingCancellationTokenSource;
        CancellationTokenSource messageProcessingCancellationTokenSource;
        int maxConcurrency;
        RepeatedFailuresOverTimeCircuitBreaker messageReceivingCircuitBreaker;
        RepeatedFailuresOverTimeCircuitBreaker messageProcessingCircuitBreaker;
        Task messageReceivingTask;
        ProcessStrategy processStrategy;

        static readonly ILog Logger = LogManager.GetLogger<MessageReceiver>();
        PushRuntimeSettings limitations;


        public ISubscriptionManager Subscriptions { get; }
        public string Id => receiveSettings.Id;
    }
}
