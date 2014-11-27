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

        public string ConnectionString { get; set; }
        public Address ErrorQueue { get; set; }

        public IReceiveStrategy Create(TransactionSettings settings, Func<TransportMessage, bool> tryProcessMessageCallback)
        {
            var errorQueue = new TableBasedQueue(ErrorQueue);
            if (settings.IsTransactional)
            {
                if (settings.SuppressDistributedTransactions)
                {
                    return new NativeTransactionReceiveStrategy(ConnectionString, errorQueue, tryProcessMessageCallback, pipelineExecutor, settings);
                }
                return new AmbientTransactionReceiveStrategy(ConnectionString, errorQueue, tryProcessMessageCallback, pipelineExecutor, settings);
            }
            return new NoTransactionReceiveStrategy(ConnectionString, errorQueue, tryProcessMessageCallback);
        }
    }
}