namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using NServiceBus.Pipeline;
    using NServiceBus.Unicast.Transport;

    class ReceiveStrategyFactory
    {
        readonly PipelineExecutor pipelineExecutor;
        readonly Address errorQueueAddress;
        readonly LocalConnectionParams localConnectionParams;
        readonly Func<string, SqlConnection> sqlConnectionFactory;

        public ReceiveStrategyFactory(PipelineExecutor pipelineExecutor, LocalConnectionParams localConnectionParams, Address errorQueueAddress, Func<string, SqlConnection> sqlConnectionFactory)
        {
            this.pipelineExecutor = pipelineExecutor;
            this.errorQueueAddress = errorQueueAddress;
            this.localConnectionParams = localConnectionParams;
            this.sqlConnectionFactory = sqlConnectionFactory;
        }

        public IReceiveStrategy Create(TransactionSettings settings, Func<TransportMessage, bool> tryProcessMessageCallback)
        {
            var errorQueue = new TableBasedQueue(errorQueueAddress, localConnectionParams.Schema);

            if (settings.IsTransactional)
            {
                if (settings.SuppressDistributedTransactions)
                {
                    return new NativeTransactionReceiveStrategy(localConnectionParams.ConnectionString, errorQueue, tryProcessMessageCallback, sqlConnectionFactory, pipelineExecutor, settings);
                }
                return new AmbientTransactionReceiveStrategy(localConnectionParams.ConnectionString, errorQueue, tryProcessMessageCallback, sqlConnectionFactory, pipelineExecutor, settings);
            }
            return new NoTransactionReceiveStrategy(localConnectionParams.ConnectionString, errorQueue, tryProcessMessageCallback, sqlConnectionFactory);
        }
    }
}