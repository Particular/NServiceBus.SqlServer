namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using Transport;

    class TableBasedQueueDispatcher : IQueueDispatcher
    {
        public TableBasedQueueDispatcher(SqlConnectionFactory connectionFactory, ITableBasedQueueOperationsReader queueOperationsReader)
        {
            this.connectionFactory = connectionFactory;
            this.queueOperationsReader = queueOperationsReader;
        }

        public async Task DispatchAsIsolated(List<UnicastTransportOperation> operations)
        {
            if (operations.Count == 0)
            {
                return;
            }
#if NET452
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                await Send(operations, connection, null).ConfigureAwait(false);

                scope.Complete();
            }
#else
            using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var tx = connection.BeginTransaction())
            {
                await Send(operations, connection, tx).ConfigureAwait(false);
                tx.Commit();
                scope.Complete();
            }
#endif

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

            transportTransaction.TryGet(out SqlConnection sqlTransportConnection);
            transportTransaction.TryGet(out SqlTransaction sqlTransportTransaction);
            transportTransaction.TryGet(out Transaction ambientTransaction);

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
                var queueOperation = queueOperationsReader.Get(operation);
                await queueOperation(connection, transaction).ConfigureAwait(false);
            }
        }

        static bool InReceiveWithNoTransactionMode(TransportTransaction transportTransaction)
        {
            transportTransaction.TryGet(out SqlTransaction nativeTransaction);

            transportTransaction.TryGet(out Transaction ambientTransaction);

            return nativeTransaction == null && ambientTransaction == null;
        }

        static bool InReceiveOnlyTransportTransactionMode(TransportTransaction transportTransaction)
        {
            return transportTransaction.TryGet(ProcessWithNativeTransaction.ReceiveOnlyTransactionMode, out bool _);
        }

        SqlConnectionFactory connectionFactory;
        ITableBasedQueueOperationsReader queueOperationsReader;
    }
}