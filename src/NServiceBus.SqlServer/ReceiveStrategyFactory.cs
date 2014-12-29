namespace NServiceBus.Transports.SQLServer
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Unicast.Transport;

    class ReceiveStrategyFactory
    {
        readonly PipelineExecutor pipelineExecutor;

        public ReceiveStrategyFactory(PipelineExecutor pipelineExecutor)
        {
            this.pipelineExecutor = pipelineExecutor;
        }

        public ConnectionParams ConnectionInfo { get; set; }
        public Address ErrorQueue { get; set; }

        public IReceiveStrategy Create(TransactionSettings settings, Func<TransportMessage, bool> tryProcessMessageCallback)
        {
            var errorQueue = new TableBasedQueue(ErrorQueue, ConnectionInfo.Schema);
            if (settings.IsTransactional)
            {
                if (settings.SuppressDistributedTransactions)
                {
                    return new NativeTransactionReceiveStrategy(ConnectionInfo.ConnectionString, errorQueue, tryProcessMessageCallback, pipelineExecutor, settings);
                }
                return new AmbientTransactionReceiveStrategy(ConnectionInfo.ConnectionString, errorQueue, tryProcessMessageCallback, pipelineExecutor, settings);
            }
            return new NoTransactionReceiveStrategy(ConnectionInfo.ConnectionString, errorQueue, tryProcessMessageCallback);
        }
    }
}