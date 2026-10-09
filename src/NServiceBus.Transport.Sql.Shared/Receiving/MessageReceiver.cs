namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class MessageReceiver : IMessageReceiver
    {
        public MessageReceiver(
            TransportDefinition transport,
            string receiverId,
            string receiveAddress,
            string errorQueueAddress,
            Action<string, Exception, CancellationToken> criticalErrorAction,
            Func<TransportTransactionMode, ProcessStrategy> processStrategyFactory,
            Func<string, TableBasedQueue> queueFactory,
            IPurgeQueues queuePurger,
            IPeekMessagesInQueue queuePeeker,
            TimeSpan? headSweepInterval,
            TimeSpan waitTimeCircuitBreaker,
            ISubscriptionManager subscriptionManager,
            bool purgeAllMessagesOnStartup,
            IExceptionClassifier exceptionClassifier,
            TimeProvider timeProvider)
        {
            this.transport = transport;
            this.processStrategyFactory = processStrategyFactory;
            this.queuePurger = queuePurger;
            this.queueFactory = queueFactory;
            this.queuePeeker = queuePeeker;
            this.waitTimeCircuitBreaker = waitTimeCircuitBreaker;
            this.errorQueueAddress = errorQueueAddress;
            this.criticalErrorAction = criticalErrorAction;
            this.purgeAllMessagesOnStartup = purgeAllMessagesOnStartup;
            this.exceptionClassifier = exceptionClassifier;
            this.timeProvider = timeProvider;
            if (headSweepInterval is { } interval)
            {
                this.headSweepInterval = interval > MinimumHeadSweepInterval ? interval : MinimumHeadSweepInterval;
            }
            receiveState = new ReceiveState(anchoringEnabled: headSweepInterval.HasValue);
            Subscriptions = subscriptionManager;
            Id = receiverId;
            ReceiveAddress = receiveAddress;
        }

        public virtual async Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError,
            CancellationToken cancellationToken = default)
        {
            this.limitations = limitations;

            processStrategy = processStrategyFactory(transport.TransportTransactionMode);

            messageReceivingCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("message receiving",
                waitTimeCircuitBreaker,
                ex => criticalErrorAction("Failed to peek " + ReceiveAddress, ex,
                    messageProcessingCancellationTokenSource.Token));
            messageProcessingCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("message processing",
                waitTimeCircuitBreaker,
                ex => criticalErrorAction("Failed to receive from " + ReceiveAddress, ex,
                    messageProcessingCancellationTokenSource.Token));

            inputQueue = queueFactory(ReceiveAddress);
            errorQueue = queueFactory(errorQueueAddress);

            processStrategy.Init(inputQueue, errorQueue, onMessage, onError, criticalErrorAction);

            if (purgeAllMessagesOnStartup)
            {
                try
                {
                    var purgedRowsCount = await queuePurger.Purge(inputQueue, cancellationToken).ConfigureAwait(false);

                    Logger.InfoFormat("{0:N0} messages purged from queue {1}", purgedRowsCount, ReceiveAddress);
                }
                catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
                {
                    Logger.Warn("Failed to purge input queue on startup.", ex);
                }
            }
        }

        public Task StartReceive(CancellationToken cancellationToken = default)
        {
            inputQueue.FormatPeekCommand();
            maxConcurrency = limitations.MaxConcurrency;
            concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency);

            lastHeadSweep = timeProvider.GetTimestamp();
            messageReceivingCancellationTokenSource = new CancellationTokenSource();
            messageProcessingCancellationTokenSource = new CancellationTokenSource();
            messageReceivingCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("message receiving", waitTimeCircuitBreaker, ex => criticalErrorAction("Failed to peek " + ReceiveAddress, ex, messageProcessingCancellationTokenSource.Token));
            messageProcessingCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("message processing", waitTimeCircuitBreaker, ex => criticalErrorAction("Failed to receive from " + ReceiveAddress, ex, messageProcessingCancellationTokenSource.Token));

            // Task.Run() so the call returns immediately instead of waiting for the first await or return down the call stack
            messageReceivingTask =
                Task.Run(() => ReceiveMessagesAndSwallowExceptions(messageReceivingCancellationTokenSource.Token),
                    CancellationToken.None);

            return Task.CompletedTask;
        }

        public async Task ChangeConcurrency(PushRuntimeSettings newLimitations,
            CancellationToken cancellationToken = default)
        {
            SemaphoreSlim oldLimiter;
            int oldMaxConcurrency;

            lock (lockObject)
            {
                oldLimiter = concurrencyLimiter;
                oldMaxConcurrency = maxConcurrency;
                concurrencyLimiter = new SemaphoreSlim(newLimitations.MaxConcurrency);
                limitations = newLimitations;
                maxConcurrency = limitations.MaxConcurrency;
            }

            try
            {
                //Drain and dispose of the old semaphore
                while (oldLimiter.CurrentCount != oldMaxConcurrency)
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }

                oldLimiter.Dispose();
            }
            catch (Exception ex) when (exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
            {
                //Ignore, we are stopping anyway
            }
        }

        public async Task StopReceive(CancellationToken cancellationToken = default)
        {
            if (messageReceivingCancellationTokenSource == null)
            {
                // already stopped or never started
                return;
            }

            await messageReceivingCancellationTokenSource.CancelAsync().ConfigureAwait(false);

            await using (cancellationToken.Register(() => messageProcessingCancellationTokenSource?.Cancel()))
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
            messageReceivingCancellationTokenSource.Dispose();
            messageReceivingCancellationTokenSource = null;
            messageProcessingCancellationTokenSource.Dispose();
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
                    catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, messageReceivingCancellationToken))
                    {
                        Logger.Error("Message receiving failed", ex);
                        await messageReceivingCircuitBreaker.Failure(ex, messageReceivingCancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (exceptionClassifier.IsOperationCancelled(ex, messageReceivingCancellationToken))
                {
                    // private token, receiver is being stopped, log the exception in case the stack trace is ever needed for debugging
                    Logger.Debug("Operation canceled while stopping the message receiver.", ex);
                    break;
                }
            }
        }

        async Task ReceiveMessages(CancellationToken messageReceivingCancellationToken)
        {
            // each receive of the previous batch reports into the state before signalling its latch
            if (!receiveState.BeginBatch())
            {
                await queuePeeker.WaitForPeekDelay(messageReceivingCancellationToken).ConfigureAwait(false);
            }

            var peekResult = await queuePeeker
                .Peek(inputQueue, messageReceivingCircuitBreaker, messageReceivingCancellationToken)
                .ConfigureAwait(false);

            if (peekResult.MessageCount == 0)
            {
                return;
            }

            receiveState.ApplyPeekResult(peekResult.LowestRowVersion);

            messageReceivingCancellationToken.ThrowIfCancellationRequested();

            // We cannot dispose this token source because of potential race conditions of concurrent processing
            var stopBatchCancellationSource = new CancellationTokenSource();

            // If either the receiving or processing circuit breakers are triggered, start only one message processing task at a time.
            var maximumConcurrentProcessing =
                messageProcessingCircuitBreaker.IsTriggered || messageReceivingCircuitBreaker.IsTriggered
                    ? 1
                    : peekResult.MessageCount;

            var receiveLatch = new ReceiveCountdownEvent(maximumConcurrentProcessing);
            for (var i = 0; i < maximumConcurrentProcessing; i++)
            {
                if (stopBatchCancellationSource.IsCancellationRequested)
                {
                    break;
                }

                var localConcurrencyLimiter = concurrencyLimiter;

                await localConcurrencyLimiter.WaitAsync(messageReceivingCancellationToken).ConfigureAwait(false);

                // A busy queue never ends a batch, so the peek alone would rarely find rows stranded behind the
                // anchor. Periodically sweeping from the head finds them wherever they are, including rows the
                // batch's peek skipped because they were locked in flight at the time.
                if (headSweepInterval is { } interval && timeProvider.GetElapsedTime(lastHeadSweep) >= interval)
                {
                    receiveState.SweepFromHead();
                    lastHeadSweep = timeProvider.GetTimestamp();
                }

                _ = ProcessMessagesSwallowExceptionsAndReleaseConcurrencyLimiter(stopBatchCancellationSource,
                    localConcurrencyLimiter, receiveLatch, messageProcessingCancellationTokenSource.Token);
            }

            // Wait for all receive operations to complete before returning (and thus peeking again)
            await receiveLatch.WaitAsync(stopBatchCancellationSource.Token).ConfigureAwait(false);
        }

        async Task ProcessMessagesSwallowExceptionsAndReleaseConcurrencyLimiter(
            CancellationTokenSource stopBatchCancellationTokenSource, SemaphoreSlim localConcurrencyLimiter,
            ReceiveCountdownEvent receiveLatch, CancellationToken messageProcessingCancellationToken)
        {
            using var latchSignaler = receiveLatch.GetSignaler();
            var receiveAttempt = new ReceiveAttempt(inputQueue, receiveState, latchSignaler, stopBatchCancellationTokenSource);
            try
            {
                try
                {
                    // We need to force the method to continue asynchronously because SqlConnection
                    // in combination with TransactionScope will apply connection pooling and enlistment synchronous in ctor.
                    await Task.Yield();

                    var outcome = await processStrategy.ProcessMessage(receiveAttempt, messageProcessingCancellationToken)
                        .ConfigureAwait(false);
                    receiveAttempt.Settle(outcome);

                    messageProcessingCircuitBreaker.Success();
                }
                catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, messageProcessingCancellationToken))
                {
                    Logger.Warn("Message processing failed", ex);

                    // a received row that was not processed has rolled back; with no row received there is nothing to settle
                    receiveAttempt.Settle(ProcessOutcome.RolledBack);

                    if (!exceptionClassifier.IsDeadlockException(ex))
                    {
                        await messageProcessingCircuitBreaker.Failure(ex, messageProcessingCancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) when (exceptionClassifier.IsOperationCancelled(ex, messageProcessingCancellationToken))
            {
                Logger.Debug("Message processing canceled.", ex);
            }
            finally
            {
                localConcurrencyLimiter.Release();
            }
        }

        protected TableBasedQueue inputQueue;
        TableBasedQueue errorQueue;
        readonly object lockObject = new();
        readonly TransportDefinition transport;
        readonly string errorQueueAddress;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;
        readonly Func<TransportTransactionMode, ProcessStrategy> processStrategyFactory;
        readonly IPurgeQueues queuePurger;
        readonly Func<string, TableBasedQueue> queueFactory;
        readonly IPeekMessagesInQueue queuePeeker;
        readonly bool purgeAllMessagesOnStartup;
        readonly IExceptionClassifier exceptionClassifier;
        readonly TimeProvider timeProvider;
        readonly TimeSpan? headSweepInterval;
        long lastHeadSweep;
        TimeSpan waitTimeCircuitBreaker;
        readonly ReceiveState receiveState;
        static readonly TimeSpan MinimumHeadSweepInterval = TimeSpan.FromSeconds(1);
        volatile SemaphoreSlim concurrencyLimiter;
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
        public string Id { get; }
        public string ReceiveAddress { get; }
    }
}