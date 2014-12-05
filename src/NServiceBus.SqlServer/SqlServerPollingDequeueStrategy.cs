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
        /// Name of the schema where queues are located
        /// </summary>
        public string SchemaName { get; set; }

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
            var actualConcurrencyLevel = maximumConcurrencyLevel + SecondaryReceiveSettings.MaximumConcurrencyLevel;
            tokenSource = new CancellationTokenSource();

            // We need to add an extra one because if we fail and the count is at zero already, it doesn't allow us to add one more.
            countdownEvent = new CountdownEvent(actualConcurrencyLevel + 1);

            for (var i = 0; i < maximumConcurrencyLevel; i++)
            {
                StartReceiveThread(new TableBasedQueue(primaryAddress, SchemaName));
            }
            for (var i = 0; i < SecondaryReceiveSettings.MaximumConcurrencyLevel; i++)
            {
                StartReceiveThread(new TableBasedQueue(SecondaryReceiveSettings.ReceiveQueue.GetTableName(), SchemaName));
            }
            if (SecondaryReceiveSettings.IsEnabled)
            {
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
            countdownEvent.Signal();
            countdownEvent.Wait();
        }

        public void Dispose()
        {
            // Injected
        }

        void StartReceiveThread(TableBasedQueue queue)
        {
            var token = tokenSource.Token;

            Task.Factory
                .StartNew(ReceiveLoop, new ReceiveLoppArgs(token, queue), token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(t =>
                {
                    t.Exception.Handle(ex =>
                    {
                        Logger.Warn("An exception occurred when connecting to the configured SqlServer", ex);
                        circuitBreaker.Failure(ex);
                        return true;
                    });

                    if (!tokenSource.IsCancellationRequested)
                    {
                        if (countdownEvent.TryAddCount())
                        {
                            StartReceiveThread(queue);
                        }
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        class ReceiveLoppArgs
        {
            public readonly CancellationToken Token;
            public readonly TableBasedQueue Queue;

            public ReceiveLoppArgs(CancellationToken token, TableBasedQueue queue)
            {
                Token = token;
                Queue = queue;
            }
        }

        void ReceiveLoop(object obj)
        {
            try
            {
                var args = (ReceiveLoppArgs)obj;
                var backOff = new BackOff(1000);

                while (!args.Token.IsCancellationRequested)
                {
                    var result = ReceiveResult.NoMessage();
                    try
                    {
                        result = receiveStrategy.TryReceiveFrom(args.Queue);
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
            finally
            {
                countdownEvent.Signal();
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

        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        CountdownEvent countdownEvent;
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