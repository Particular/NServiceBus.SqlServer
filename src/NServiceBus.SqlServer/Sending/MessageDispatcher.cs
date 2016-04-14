namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(SqlConnectionFactory connectionFactory, QueueAddressParser addressParser)
        {
            this.connectionFactory = connectionFactory;
            this.addressParser = addressParser;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, ContextBag context)
        {
            var isolatedConsistencyOperations = operations.UnicastTransportOperations.Where(o => o.RequiredDispatchConsistency == DispatchConsistency.Isolated).ToList();
            await DispatchAsIsolated(isolatedConsistencyOperations).ConfigureAwait(false);

            var defaultConsistencyOperations = operations.UnicastTransportOperations.Where(o => o.RequiredDispatchConsistency == DispatchConsistency.Default).ToList();
            await DispatchAsNonIsolated(context, defaultConsistencyOperations).ConfigureAwait(false);
        }

        async Task DispatchAsNonIsolated(ContextBag context, List<UnicastTransportOperation> defaultConsistencyOperations)
        {
            TransportTransaction transportTransaction;
            var transportTransactionExists = context.TryGet(out transportTransaction);

            if (transportTransactionExists == false || InReceiveOnlyTransportTransactionMode(transportTransaction))
            {
                await DispatchOperationsWithNewConnectionAndTransaction(defaultConsistencyOperations).ConfigureAwait(false);
                return;
            }

            await DispatchUsingReceiveTransaction(transportTransaction, defaultConsistencyOperations).ConfigureAwait(false);
        }

        async Task DispatchUsingReceiveTransaction(TransportTransaction transportTransaction, List<UnicastTransportOperation> defaultConsistencyOperations)
        {
            SqlConnection sqlTransportConnection;
            SqlTransaction sqlTransportTransaction;

            transportTransaction.TryGet(out sqlTransportConnection);
            transportTransaction.TryGet(out sqlTransportTransaction);

            foreach (var operation in defaultConsistencyOperations)
            {
                var destination = addressParser.Parse(operation.Destination);
                var queue = new TableBasedQueue(destination);

                await queue.SendMessage(operation.Message, sqlTransportConnection, sqlTransportTransaction).ConfigureAwait(false);
            }
        }

        async Task DispatchOperationsWithNewConnectionAndTransaction(List<UnicastTransportOperation> defaultConsistencyOperations)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var operation in defaultConsistencyOperations)
                {
                    var destination = addressParser.Parse(operation.Destination);
                    var queue = new TableBasedQueue(destination);

                    await queue.SendMessage(operation.Message, connection, transaction).ConfigureAwait(false);
                }

                transaction.Commit();
            }
        }

        async Task DispatchAsIsolated(List<UnicastTransportOperation> isolatedConsistencyOperations)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                foreach (var operation in isolatedConsistencyOperations)
                {
                    var destination = addressParser.Parse(operation.Destination);
                    var queue = new TableBasedQueue(destination);

                    await queue.SendMessage(operation.Message, connection, null).ConfigureAwait(false);
                }

                scope.Complete();
            }
        }

        static bool InReceiveOnlyTransportTransactionMode(TransportTransaction transportTransaction)
        {
            bool inReceiveMode;

            return transportTransaction.TryGet(ReceiveWithReceiveOnlyTransaction.ReceiveOnlyTransactionMode, out inReceiveMode);
        }

        QueueAddressParser addressParser;

        SqlConnectionFactory connectionFactory;
    }
}