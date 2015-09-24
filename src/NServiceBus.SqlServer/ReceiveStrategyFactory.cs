namespace NServiceBus.Transports.SQLServer
{
    using System;
    using NServiceBus.Unicast.Transport;

    class ReceiveStrategyFactory
    {
        readonly IConnectionStore connectionStore;
        readonly Address errorQueueAddress;
        readonly LocalConnectionParams localConnectionParams;
        readonly ConnectionFactory sqlConnectionFactory;

        public ReceiveStrategyFactory(IConnectionStore connectionStore, LocalConnectionParams localConnectionParams, Address errorQueueAddress, ConnectionFactory sqlConnectionFactory)
        {
            this.connectionStore = connectionStore;
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
                    return new NativeTransactionReceiveStrategy(localConnectionParams.ConnectionString, errorQueue, tryProcessMessageCallback, sqlConnectionFactory, connectionStore, settings);
                }
                return new AmbientTransactionReceiveStrategy(localConnectionParams.ConnectionString, errorQueue, tryProcessMessageCallback, sqlConnectionFactory, connectionStore, settings);
            }
            return new NoTransactionReceiveStrategy(localConnectionParams.ConnectionString, errorQueue, tryProcessMessageCallback, sqlConnectionFactory);
        }
    }
}