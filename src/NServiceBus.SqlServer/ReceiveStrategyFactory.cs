namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Transactions;
    using NServiceBus.Pipeline;
    using NServiceBus.Unicast.Transport;

    class ReceiveStrategyFactory
    {
        public IReceiveStrategy ChooseReceiveStrategy()
        {
            var transactionOptions = new TransactionOptions
            {
                IsolationLevel = transactionSettings.IsolationLevel,
                Timeout = transactionSettings.TransactionTimeout
            };
            if (transactionSettings.IsTransactional)
            {
                if (transactionSettings.SuppressDistributedTransactions)
                {
                    return new NativeTransactionReceiveStrategy(tryProcessMessage, transportMessageReader, connectionString, pipelineExecutor, transactionOptions);
                }
                return new TransactionScopeReceiveStrategy(tryProcessMessage, transportMessageReader, connectionString, pipelineExecutor, transactionOptions);
            }
            return new NoTransactionReceiveStrategy(tryProcessMessage, transportMessageReader, connectionString, pipelineExecutor, transactionOptions);
        }

        public ReceiveStrategyFactory(Func<TransportMessage, bool> tryProcessMessage, TransportMessageReader transportMessageReader, string connectionString, PipelineExecutor pipelineExecutor, TransactionSettings transactionSettings)
        {
            this.tryProcessMessage = tryProcessMessage;
            this.transportMessageReader = transportMessageReader;
            this.connectionString = connectionString;
            this.pipelineExecutor = pipelineExecutor;
            this.transactionSettings = transactionSettings;
        }

        private Func<TransportMessage, bool> tryProcessMessage;
        private TransportMessageReader transportMessageReader;
        private String connectionString;
        private PipelineExecutor pipelineExecutor;
        private TransactionSettings transactionSettings;
    }
}