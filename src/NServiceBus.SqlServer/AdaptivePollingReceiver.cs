namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using NServiceBus.CircuitBreakers;
    using NServiceBus.Logging;

    class AdaptivePollingReceiver : AdaptiveExecutor<ReceiveResult>
    {
        /// <summary>
        /// Controls the ramping up of additional threads in case one of the current threads is processing a slow message.
        /// If the processing takes more than a number of seconds, an additional thread is ramped up. Such thing can happen only once per processing a single message.
        /// </summary>
        const int SlowTaskThresholdInMilliseconds = 5000;
        /// <summary>
        /// The maximum number of failures of receive for a given thread before it decides to commit suicide.
        /// </summary>
        const int MaximumConsecutiveFailures = 7;
        /// <summary>
        /// The minimum number of successful message processing attempts for a given thread before it tries to ramp up another thread.
        /// </summary>
        const int MinimumConsecutiveSuccesses = 5;
        /// <summary>
        /// The maximum time for a thread to wait if a previous receive operation failed.
        /// </summary>
        const int MaximumBackOffTimeMilliseconds = 1000;

        public AdaptivePollingReceiver(
            IReceiveStrategy receiveStrategy, 
            TableBasedQueue queue, 
            Action<TransportMessage, Exception> endProcessMessage, 
            RepeatedFailuresOverTimeCircuitBreaker circuitBreaker,
            TransportNotifications transportNotifications)
            : base(queue.ToString(), circuitBreaker, transportNotifications, TimeSpan.FromMilliseconds(SlowTaskThresholdInMilliseconds))
        {
            this.receiveStrategy = receiveStrategy;
            this.queue = queue;
            this.endProcessMessage = endProcessMessage;
            this.transportNotifications = transportNotifications;
        }

        public override void Start(int maximumConcurrency, CancellationToken token)
        {
            Logger.InfoFormat("Receiver for queue '{0}' started with maximum concurrency '{1}'", queue, maximumConcurrency);
            base.Start(maximumConcurrency, token);
        }

        protected override ReceiveResult Init()
        {
            return ReceiveResult.NoMessage();
        }

        protected override ReceiveResult Try(ReceiveResult initialValue, out bool success)
        {
            var result = receiveStrategy.TryReceiveFrom(queue);
            success = result.HasReceivedMessage;
            return result;
        }

        protected override void Finally(ReceiveResult value)
        {
            if (value.HasReceivedMessage)
            {
                endProcessMessage(value.Message, value.Exception);
            }
        }

        protected override void HandleException(Exception ex)
        {
            Logger.Warn("An exception occurred when connecting to the configured SQLServer instance", ex);
        }

        protected override IRampUpController CreateRampUpController(Action rampUpCallback)
        {
            return new ReceiveRampUpController(rampUpCallback, transportNotifications, queue.ToString(), MaximumConsecutiveFailures, MinimumConsecutiveSuccesses);
        }

        protected override IBackOffStrategy CreateBackOffStrategy()
        {
            return new BoundedExponentialBackOff(MaximumBackOffTimeMilliseconds);
        }

        readonly IReceiveStrategy receiveStrategy;
        readonly TableBasedQueue queue;
        readonly Action<TransportMessage, Exception> endProcessMessage;
        readonly TransportNotifications transportNotifications;

        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerPollingDequeueStrategy)); //Intentionally using other type here
    }
}