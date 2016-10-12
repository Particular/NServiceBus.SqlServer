namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Transport;

    class TableBasedQueueDispatcher : IQueueDispatcher
    {
        SqlConnectionFactory connectionFactory;
        DelayedMessageTable delayedMessageTable;
        QueueAddressParser addressParser;

        public TableBasedQueueDispatcher(SqlConnectionFactory connectionFactory, DelayedMessageTable delayedMessageTable, QueueAddressParser addressParser)
        {
            this.connectionFactory = connectionFactory;
            this.delayedMessageTable = delayedMessageTable;
            this.addressParser = addressParser;
        }

        public async Task DispatchAsIsolated(UnicastTransportOperation[] operations)
        {
            if (operations.Length == 0)
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

        public async Task DispatchAsNonIsolated(UnicastTransportOperation[] operations, TransportTransaction transportTransaction)
        {
            if (operations.Length == 0)
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


        async Task DispatchOperationsWithNewConnectionAndTransaction(UnicastTransportOperation[] operations)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                if (operations.Length == 1)
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

        async Task DispatchUsingReceiveTransaction(TransportTransaction transportTransaction, UnicastTransportOperation[] operations)
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

        async Task Send(UnicastTransportOperation[] operations, SqlConnection connection, SqlTransaction transaction)
        {
            foreach (var operation in operations)
            {
                DateTime? due;
                if (delayedMessageTable == null || !(due = GetDueTime(operation)).HasValue)
                {
                    var queueAddress = addressParser.Parse(operation.Destination);
                    var queue = new TableBasedQueue(queueAddress);
                    await queue.Send(operation.Message, connection, transaction).ConfigureAwait(false);
                }
                else
                {
                    await delayedMessageTable.Store(operation.Message, due.Value, operation.Destination, connection, transaction).ConfigureAwait(false);
                }
            }
        }

        static DateTime? GetDueTime(IOutgoingTransportOperation operation)
        {
            DoNotDeliverBefore doNotDeliverBefore;
            DelayDeliveryWith delayDeliveryWith;
            if (TryGetConstraint(operation, out doNotDeliverBefore))
            {
                return doNotDeliverBefore.At;
            }
            if (TryGetConstraint(operation, out delayDeliveryWith))
            {
                return DateTime.UtcNow + delayDeliveryWith.Delay;
            }
            return null;
        }

        static bool TryGetConstraint<T>(IOutgoingTransportOperation operation, out T constraint) where T : DeliveryConstraint
        {
            constraint = operation.DeliveryConstraints.OfType<T>().FirstOrDefault();
            return constraint != null;
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
    }
}