namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Transactions;
    using NServiceBus.Pipeline;

    class ReceiveStrategyBase
    {
        protected ReceiveStrategyBase(Func<TransportMessage, bool> tryProcessMessage, TransportMessageReader transportMessageReader, string connectionString, PipelineExecutor pipelineExecutor, TransactionOptions transactionOptions)
        {
            this.tryProcessMessage = tryProcessMessage;
            this.transportMessageReader = transportMessageReader;
            this.connectionString = connectionString;
            this.pipelineExecutor = pipelineExecutor;
            this.transactionOptions = transactionOptions;
        }

        protected Func<TransportMessage, bool> tryProcessMessage;
        protected TransportMessageReader transportMessageReader;
        protected String connectionString;
        protected PipelineExecutor pipelineExecutor;
        protected TransactionOptions transactionOptions;
    }
}