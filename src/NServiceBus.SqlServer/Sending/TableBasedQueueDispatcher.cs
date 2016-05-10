namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Transports;

    class TableBasedQueueDispatcher : IQueueDispatcher
    {
        SqlConnectionFactory connectionFactory;

        public TableBasedQueueDispatcher(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task DispatchAsIsolated(List<MessageWithAddress> operations)
        {
            if (operations.Count == 0)
            {
                return;
            }
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

        public async Task DispatchAsNonIsolated(List<MessageWithAddress> operations, ContextBag context)
        {
            if (operations.Count == 0)
            {
                return;
            }
            TransportTransaction transportTransaction;
            var transportTransactionExists = context.TryGet(out transportTransaction);

            if (transportTransactionExists == false || InReceiveOnlyTransportTransactionMode(transportTransaction))
            {
                await DispatchOperationsWithNewConnectionAndTransaction(operations).ConfigureAwait(false);
                return;
            }

            await DispatchUsingReceiveTransaction(transportTransaction, operations).ConfigureAwait(false);
        }


        async Task DispatchOperationsWithNewConnectionAndTransaction(List<MessageWithAddress> defaultConsistencyOperations)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var operation in defaultConsistencyOperations)
                {
                    var queue = new TableBasedQueue(operation.Address);
                    await queue.Send(operation.Message, connection, transaction).ConfigureAwait(false);
                }
                transaction.Commit();
            }
        }

        async Task DispatchUsingReceiveTransaction(TransportTransaction transportTransaction, List<MessageWithAddress> defaultConsistencyOperations)
        {
            SqlConnection sqlTransportConnection;
            SqlTransaction sqlTransportTransaction;

            transportTransaction.TryGet(out sqlTransportConnection);
            transportTransaction.TryGet(out sqlTransportTransaction);

            foreach (var operation in defaultConsistencyOperations)
            {
                var queue = new TableBasedQueue(operation.Address);

                await queue.Send(operation.Message, sqlTransportConnection, sqlTransportTransaction).ConfigureAwait(false);
            }
        }

        static bool InReceiveOnlyTransportTransactionMode(TransportTransaction transportTransaction)
        {
            bool inReceiveMode;
            return transportTransaction.TryGet(ReceiveWithReceiveOnlyTransaction.ReceiveOnlyTransactionMode, out inReceiveMode);
        }
    }
}