namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;

    class TableBasedQueueDispatcher
    {
        SqlConnectionFactory connectionFactory;

        public TableBasedQueueDispatcher(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public virtual async Task DispatchOperationsWithNewConnectionAndTransaction(List<MessageWithAddress> defaultConsistencyOperations)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var operation in defaultConsistencyOperations)
                {
                    var queue = new TableBasedQueue(operation.Address);

                    await queue.SendMessage(operation.Message, connection, transaction).ConfigureAwait(false);
                }

                transaction.Commit();
            }
        }

        public virtual async Task DispatchUsingReceiveTransaction(TransportTransaction transportTransaction, List<MessageWithAddress> defaultConsistencyOperations)
        {
            SqlConnection sqlTransportConnection;
            SqlTransaction sqlTransportTransaction;

            transportTransaction.TryGet(out sqlTransportConnection);
            transportTransaction.TryGet(out sqlTransportTransaction);

            foreach (var operation in defaultConsistencyOperations)
            {
                var queue = new TableBasedQueue(operation.Address);

                await queue.SendMessage(operation.Message, sqlTransportConnection, sqlTransportTransaction).ConfigureAwait(false);
            }
        }

        public virtual async Task DispatchAsIsolated(List<MessageWithAddress> isolatedConsistencyOperations)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                foreach (var operation in isolatedConsistencyOperations)
                {
                    var queue = new TableBasedQueue(operation.Address);

                    await queue.SendMessage(operation.Message, connection, null).ConfigureAwait(false);
                }

                scope.Complete();
            }
        }
    }
}