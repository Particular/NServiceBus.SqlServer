namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using Transport;

    class TableBasedQueueDispatcher : IQueueDispatcher
    {
        public TableBasedQueueDispatcher(SqlConnectionFactory connectionFactory, QueueAddressTranslator addressTranslator)
        {
            this.connectionFactory = connectionFactory;
            this.addressTranslator = addressTranslator;
        }

        public async Task DispatchAsIsolated(List<UnicastTransportOperation> operations)
        {
            if (operations.Count == 0)
            {
                return;
            }
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                await Send(operations, connection, null).ConfigureAwait(false);

                scope.Complete();
            }
        }

        public async Task DispatchAsNonIsolated(List<UnicastTransportOperation> operations, TransportTransaction transportTransaction)
        {
            if (operations.Count == 0)
            {
                return;
            }

            if (InReceiveWithNoTransactionMode(transportTransaction) || InReceiveOnlyTransportTransactionMode(transportTransaction))
            {
                await DispatchOperationsWithNewConnectionAndTransaction(operations).ConfigureAwait(false);
                return;
            }

            await DispatchUsingReceiveTransaction(transportTransaction, operations).ConfigureAwait(false);
        }


        async Task DispatchOperationsWithNewConnectionAndTransaction(List<UnicastTransportOperation> operations)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                if (operations.Count == 1)
                {
                    await Send(operations, connection, null).ConfigureAwait(false);
                    return;
                }

                using (var transaction = connection.BeginTransaction())
                {
                    await Send(operations, connection, transaction).ConfigureAwait(false);
                    transaction.Commit();
                }
            }
        }

        async Task DispatchUsingReceiveTransaction(TransportTransaction transportTransaction, List<UnicastTransportOperation> operations)
        {
            SqlConnection sqlTransportConnection;
            SqlTransaction sqlTransportTransaction;
            Transaction ambientTransaction;

            transportTransaction.TryGet(out sqlTransportConnection);
            transportTransaction.TryGet(out sqlTransportTransaction);
            transportTransaction.TryGet(out ambientTransaction);

            if (ambientTransaction != null)
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                    await Send(operations, connection, null).ConfigureAwait(false);
                }
            }
            else
            {
                await Send(operations, sqlTransportConnection, sqlTransportTransaction).ConfigureAwait(false);
            }
        }

        async Task Send(List<UnicastTransportOperation> operations, SqlConnection connection, SqlTransaction transaction)
        {
            foreach (var operation in operations)
            {
                var address = addressTranslator.Parse(operation.Destination);
                var queue = new TableBasedQueue(address.QualifiedTableName, address.Address);
                await queue.Send(operation.Message.Headers, operation.Message.Body, connection, transaction).ConfigureAwait(false);
            }
        }

        static bool InReceiveWithNoTransactionMode(TransportTransaction transportTransaction)
        {
            SqlTransaction nativeTransaction;
            transportTransaction.TryGet(out nativeTransaction);

            Transaction ambientTransaction;
            transportTransaction.TryGet(out ambientTransaction);

            return nativeTransaction == null && ambientTransaction == null;
        }

        static bool InReceiveOnlyTransportTransactionMode(TransportTransaction transportTransaction)
        {
            bool inReceiveMode;
            return transportTransaction.TryGet(ReceiveWithNativeTransaction.ReceiveOnlyTransactionMode, out inReceiveMode);
        }

        SqlConnectionFactory connectionFactory;
        QueueAddressTranslator addressTranslator;
    }
}