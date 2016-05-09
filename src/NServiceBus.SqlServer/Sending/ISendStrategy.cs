namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;

    interface IDispatchStrategy
    {
        Task Dispatch(List<MessageWithAddress> operations);
    }
    
    class IsolatedDispatchSendStrategy : IDispatchStrategy
    {
        public IsolatedDispatchSendStrategy(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task Dispatch(List<MessageWithAddress> operations)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                foreach (var operation in operations)
                {
                    var queue = new TableBasedQueue(operation.Address);

                    await queue.Send(operation.Message, connection, null).ConfigureAwait(false);
                }
                scope.Complete();
            }
        }

        SqlConnectionFactory connectionFactory;
    }

    class ReceiveConnectionDispatchStrategy : IDispatchStrategy
    {
        public ReceiveConnectionDispatchStrategy(TransportTransaction transportTransaction)
        {
            this.transportTransaction = transportTransaction;
        }

        public async Task Dispatch(List<MessageWithAddress> operations)
        {
            SqlConnection sqlTransportConnection;
            SqlTransaction sqlTransportTransaction;

            transportTransaction.TryGet(out sqlTransportConnection);
            transportTransaction.TryGet(out sqlTransportTransaction);

            foreach (var operation in operations)
            {
                var queue = new TableBasedQueue(operation.Address);

                await queue.Send(operation.Message, sqlTransportConnection, sqlTransportTransaction).ConfigureAwait(false);
            }
        }

        TransportTransaction transportTransaction;
    }

    class SeparateConnectionDispatchStrategy : IDispatchStrategy
    {
        public SeparateConnectionDispatchStrategy(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task Dispatch(List<MessageWithAddress> operations)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var operation in operations)
                {
                    var queue = new TableBasedQueue(operation.Address);
                    await queue.Send(operation.Message, connection, transaction).ConfigureAwait(false);
                }
                transaction.Commit();
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}