namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using CircuitBreakers;
    using Logging;
    using NServiceBus.Features;
    using Unicast.Transport;

    /// <summary>
    ///     A polling implementation of <see cref="IDequeueMessages" />.
    /// </summary>
    class SqlServerPollingDequeueStrategy : IDequeueMessages, IDisposable
    {
        public SqlServerPollingDequeueStrategy(
            ReceiveStrategyFactory receiveStrategyFactory, IQueuePurger queuePurger, CriticalError criticalError, SecondaryReceiveConfiguration secondaryReceiveConfiguration)
        {
            this.receiveStrategyFactory = receiveStrategyFactory;
            this.queuePurger = queuePurger;
            this.secondaryReceiveConfiguration = secondaryReceiveConfiguration;
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("SqlTransportConnectivity",
                TimeSpan.FromMinutes(2),
                ex => criticalError.Raise("Repeated failures when communicating with SqlServer", ex),
                TimeSpan.FromSeconds(10));
        }

        /// <summary>
        ///     Initializes the <see cref="IDequeueMessages" />.
        /// </summary>
        /// <param name="address">The address to listen on.</param>
        /// <param name="transactionSettings">
        ///     The <see cref="TransactionSettings" /> to be used by <see cref="IDequeueMessages" />.
        /// </param>
        /// <param name="tryProcessMessage">Called when a message has been dequeued and is ready for processing.</param>
        /// <param name="endProcessMessage">
        ///     Needs to be called by <see cref="IDequeueMessages" /> after the message has been processed regardless if the
        ///     outcome was successful or not.
        /// </param>
        public void Init(Address address, TransactionSettings transactionSettings,
            Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
        {
            this.endProcessMessage = endProcessMessage;

            primaryAddress = address;
            secondaryReceiveSettings = secondaryReceiveConfiguration.GetSettings(primaryAddress.Queue);
            receiveStrategy = receiveStrategyFactory.Create(transactionSettings, tryProcessMessage);
            queuePurger.Purge(address);
        }

        /// <summary>
        ///     Starts the dequeuing of message using the specified <paramref name="maximumConcurrencyLevel" />.
        /// </summary>
        /// <param name="maximumConcurrencyLevel">
        ///     Indicates the maximum concurrency level this <see cref="IDequeueMessages" /> is able to support.
        /// </param>
        public void Start(int maximumConcurrencyLevel)
        {
            tokenSource = new CancellationTokenSource();

            primaryReceiveTaskTracker = new ReceiveTaskTracker(maximumConcurrencyLevel);
            secondaryReceiveTaskTracker = new ReceiveTaskTracker(SecondaryReceiveSettings.MaximumConcurrencyLevel);

            StartReceiveTask(new TableBasedQueue(primaryAddress), primaryReceiveTaskTracker);
            if (SecondaryReceiveSettings.IsEnabled)
            {
                StartReceiveTask(new TableBasedQueue(SecondaryReceiveSettings.ReceiveQueue.GetTableName()), secondaryReceiveTaskTracker);
                Logger.InfoFormat("Secondary receiver for queue '{0}' initiated with concurrency '{1}'", SecondaryReceiveSettings.ReceiveQueue, SecondaryReceiveSettings.MaximumConcurrencyLevel);
            }

        }

        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public void Stop()
        {
            if (tokenSource == null)
            {
                return;
            }

            tokenSource.Cancel();
            primaryReceiveTaskTracker.ShutdownAll();
            secondaryReceiveTaskTracker.ShutdownAll();
        }

        public void Dispose()
        {
            // Injected
        }

        void StartReceiveTask(TableBasedQueue queue, ReceiveTaskTracker taskTracker)
        {
            var token = tokenSource.Token;

            taskTracker.StartAndTrack(() =>
            {
                var receiveTask = Task.Factory
                    .StartNew(ReceiveLoop, new ReceiveLoppArgs(token, queue, new RampUpController(() => StartReceiveTask(queue, taskTracker))), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                receiveTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        t.Exception.Handle(ex =>
                        {
                            Logger.Warn("An exception occurred when connecting to the configured SqlServer", ex);
                            circuitBreaker.Failure(ex);
                            return true;
                        });
                    }
                    taskTracker.Forget(t);
                    if (taskTracker.HasNoTasks)
                    {
                        StartReceiveTask(queue, taskTracker);
                    }
                });
                return receiveTask;
            });
        }

        class ReceiveLoppArgs
        {
            public readonly CancellationToken Token;
            public readonly TableBasedQueue Queue;
            public readonly RampUpController RampUpController;

            public ReceiveLoppArgs(CancellationToken token, TableBasedQueue queue, RampUpController rampUpController)
            {
                Token = token;
                Queue = queue;
                RampUpController = rampUpController;
            }
        }

        void ReceiveLoop(object obj)
        {
            var args = (ReceiveLoppArgs)obj;
            var backOff = new BackOff(1000);
            var rampUpController = args.RampUpController;

            while (!args.Token.IsCancellationRequested && rampUpController.HasEnoughWork)
            {
                rampUpController.RampUpIfTooMuchWork();
                var result = ReceiveResult.NoMessage();
                try
                {
                    result = receiveStrategy.TryReceiveFrom(args.Queue);
                    if (result.HasReceivedMessage)
                    {
                        rampUpController.ReadSucceeded();
                    }
                    else
                    {
                        rampUpController.ReadFailed();
                    }
                }
                finally
                {
                    if (result.HasReceivedMessage)
                    {
                        endProcessMessage(result.Message, result.Exception);
                    }
                }

                circuitBreaker.Success();
                backOff.Wait(() => !result.HasReceivedMessage);
            }
        }

        SecondaryReceiveSettings SecondaryReceiveSettings
        {
            get
            {
                if (secondaryReceiveSettings == null)
                {
                    throw new InvalidOperationException("Cannot get secondary receive settings before Init was called.");
                }
                return secondaryReceiveSettings;
            }
        }


        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerPollingDequeueStrategy));

        ReceiveTaskTracker primaryReceiveTaskTracker;
        ReceiveTaskTracker secondaryReceiveTaskTracker;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        Action<TransportMessage, Exception> endProcessMessage;
        readonly ReceiveStrategyFactory receiveStrategyFactory;
        readonly IQueuePurger queuePurger;
        IReceiveStrategy receiveStrategy;

        readonly SecondaryReceiveConfiguration secondaryReceiveConfiguration;
        SecondaryReceiveSettings secondaryReceiveSettings;
        Address primaryAddress;
        CancellationTokenSource tokenSource;
    }
}