﻿namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using NServiceBus.CircuitBreakers;
    using NServiceBus.Logging;

    class AdaptivePollingReceiver : AdaptiveExecutor<ReceiveResult>
    {
        public AdaptivePollingReceiver(IReceiveStrategy receiveStrategy, TableBasedQueue queue, Action<TransportMessage, Exception> endProcessMessage, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker) 
            : base(circuitBreaker)
        {
            this.receiveStrategy = receiveStrategy;
            this.queue = queue;
            this.endProcessMessage = endProcessMessage;
        }

        public override void Start(int maximumConcurrency, CancellationTokenSource tokenSource)
        {
            Logger.InfoFormat("Receiver for queue '{0}' started with maximum concurrency '{1}'", queue, maximumConcurrency);
            base.Start(maximumConcurrency, tokenSource);
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

        readonly IReceiveStrategy receiveStrategy;
        readonly TableBasedQueue queue;
        readonly Action<TransportMessage, Exception> endProcessMessage;

        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlServerPollingDequeueStrategy)); //Intentionally using other type here
    }
}