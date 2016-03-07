namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using CircuitBreakers;
    using Janitor;
    using NServiceBus.Features;
    using Unicast.Transport;

    /// <summary>
    ///     A polling implementation of <see cref="IDequeueMessages" />.
    /// </summary>
    class SqlServerPollingDequeueStrategy : IDequeueMessages, IDisposable
    {
        public SqlServerPollingDequeueStrategy(
            LocalConnectionParams locaConnectionParams,
            ReceiveStrategyFactory receiveStrategyFactory, 
            IQueuePurger queuePurger, 
            SecondaryReceiveConfiguration secondaryReceiveConfiguration,
            TransportNotifications transportNotifications, 
            RepeatedFailuresOverTimeCircuitBreaker circuitBreaker,
            ConnectionFactory connectionFactory,
            PurgeExpiredMessagesParams purgeExpiredMessagesParams)
        {
            this.locaConnectionParams = locaConnectionParams;
            this.receiveStrategyFactory = receiveStrategyFactory;
            this.queuePurger = queuePurger;
            this.secondaryReceiveConfiguration = secondaryReceiveConfiguration;
            this.transportNotifications = transportNotifications;
            this.circuitBreaker = circuitBreaker;
            this.connectionFactory = connectionFactory;
            this.purgeExpiredMessagesParams = purgeExpiredMessagesParams;
        }

        /// <summary>
        ///     Initializes the <see cref="IDequeueMessages" />.
        /// </summary>
        /// <param name="primaryAddress">The address to listen on.</param>
        /// <param name="transactionSettings">
        ///     The <see cref="TransactionSettings" /> to be used by <see cref="IDequeueMessages" />.
        /// </param>
        /// <param name="tryProcessMessage">Called when a message has been dequeued and is ready for processing.</param>
        /// <param name="endProcessMessage">
        ///     Needs to be called by <see cref="IDequeueMessages" /> after the message has been processed regardless if the
        ///     outcome was successful or not.
        /// </param>
        public void Init(Address primaryAddress, TransactionSettings transactionSettings,
            Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
        {
            queuePurger.Purge(primaryAddress);

            secondaryReceiveSettings = secondaryReceiveConfiguration.GetSettings(primaryAddress.Queue);
            var receiveStrategy = receiveStrategyFactory.Create(transactionSettings, tryProcessMessage);

            var primaryQueue = new TableBasedQueue(primaryAddress, locaConnectionParams.Schema);
            primaryReceiver = new AdaptivePollingReceiver(receiveStrategy, primaryQueue, endProcessMessage, circuitBreaker, transportNotifications);

            if (secondaryReceiveSettings.IsEnabled)
            {
                var secondaryQueue = new TableBasedQueue(SecondaryReceiveSettings.ReceiveQueue.GetTableName(), locaConnectionParams.Schema);
                secondaryReceiver = new AdaptivePollingReceiver(receiveStrategy, secondaryQueue, endProcessMessage, circuitBreaker, transportNotifications);
            }
            else
            {
                secondaryReceiver = new NullExecutor();
            }

            expiredMessagesPurger = new ExpiredMessagesPurger(primaryQueue, () => connectionFactory.OpenNewConnection(locaConnectionParams.ConnectionString), purgeExpiredMessagesParams);
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

            primaryReceiver.Start(maximumConcurrencyLevel, tokenSource.Token);
            secondaryReceiver.Start(SecondaryReceiveSettings.MaximumConcurrencyLevel, tokenSource.Token);
            expiredMessagesPurger.Start(maximumConcurrencyLevel, tokenSource.Token);
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

            primaryReceiver.Stop();
            secondaryReceiver.Stop();
            expiredMessagesPurger.Stop();

            tokenSource.Dispose();
        }

        public void Dispose()
        {
            // Injected
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

        IExecutor primaryReceiver;
        IExecutor secondaryReceiver;
        IExecutor expiredMessagesPurger;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        readonly LocalConnectionParams locaConnectionParams;
        readonly ReceiveStrategyFactory receiveStrategyFactory;
        readonly IQueuePurger queuePurger;
        readonly ConnectionFactory connectionFactory;
        readonly PurgeExpiredMessagesParams purgeExpiredMessagesParams;

        readonly SecondaryReceiveConfiguration secondaryReceiveConfiguration;
        [SkipWeaving] //Do not dispose with dequeue strategy
        readonly TransportNotifications transportNotifications;
        SecondaryReceiveSettings secondaryReceiveSettings;
        CancellationTokenSource tokenSource;
    }
}