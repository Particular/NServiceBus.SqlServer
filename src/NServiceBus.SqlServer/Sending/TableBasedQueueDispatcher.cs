namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
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
                await Task.WhenAll(GetSendOperations(defaultConsistencyOperations, connection, transaction)).ConfigureAwait(false);

                transaction.Commit();
            }
        }


        public virtual async Task DispatchUsingReceiveTransaction(TransportTransaction transportTransaction, List<MessageWithAddress> defaultConsistencyOperations)
        {
            SqlConnection sqlConnection;
            SqlTransaction sqlTransaction;

            transportTransaction.TryGet(out sqlConnection);
            transportTransaction.TryGet(out sqlTransaction);

            await Task.WhenAll(GetSendOperations(defaultConsistencyOperations, sqlConnection, sqlTransaction)).ConfigureAwait(false);
        }


        public virtual async Task DispatchAsIsolated(List<MessageWithAddress> isolatedConsistencyOperations)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                await Task.WhenAll(GetSendOperations(isolatedConsistencyOperations, connection, null)).ConfigureAwait(false);
                scope.Complete();
            }
        }

        static IEnumerable<Task> GetSendOperations(List<MessageWithAddress> defaultConsistencyOperations, SqlConnection connection, SqlTransaction transaction)
        {
            return from operation in defaultConsistencyOperations
                   let queue = new TableBasedQueue(operation.Address)
                   select queue.SendMessage(operation.Message, connection, transaction);
        }
    }
}