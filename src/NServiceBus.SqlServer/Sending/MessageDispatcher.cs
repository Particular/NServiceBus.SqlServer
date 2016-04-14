namespace NServiceBus.Transports.SQLServer
{
    using System.Data.SqlClient;
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
            foreach (var operation in operations.UnicastTransportOperations)
            {
                await DispatchUnicastOperation(context, operation).ConfigureAwait(false);
            }
        }

        async Task DispatchUnicastOperation(ContextBag context, UnicastTransportOperation operation)
        {
            var destination = addressParser.Parse(operation.Destination);
            var queue = new TableBasedQueue(destination);

            //Dispatch in separate transaction even if transaction scope already exists
            if (operation.RequiredDispatchConsistency == DispatchConsistency.Isolated)
            {
                await DispatchInIsolatedTransactionScope(queue, operation).ConfigureAwait(false);

                return;
            }

            TransportTransaction transportTransaction;
            var transportTransactionExists = context.TryGet(out transportTransaction);

            if (transportTransactionExists == false || InReceiveOnlyTransportTransactionMode(transportTransaction))
            {
                await DispatchAsSeparateSendOperation(queue, operation).ConfigureAwait(false);

                return;
            }

            await DispatchInCurrentTransportTransaction(transportTransaction, queue, operation).ConfigureAwait(false);
        }

        async Task DispatchAsSeparateSendOperation(TableBasedQueue queue, UnicastTransportOperation operation)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                using (var transaction = connection.BeginTransaction())
                {
                    await queue.SendMessage(operation.Message, connection, transaction).ConfigureAwait(false);

                    transaction.Commit();
                }
            }
        }

        async Task DispatchInCurrentTransportTransaction(TransportTransaction transportTransaction, TableBasedQueue queue, UnicastTransportOperation operation)
        {
            SqlConnection connection;
            SqlTransaction transaction;

            transportTransaction.TryGet(out connection);
            transportTransaction.TryGet(out transaction);

            await queue.SendMessage(operation.Message, connection, transaction).ConfigureAwait(false);
        }

        async Task DispatchInIsolatedTransactionScope(TableBasedQueue queue, UnicastTransportOperation operation)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                    await queue.SendMessage(operation.Message, connection, null).ConfigureAwait(false);
                }

                scope.Complete();
            }
        }

        bool InReceiveOnlyTransportTransactionMode(TransportTransaction transportTransaction)
        {
            bool inReceiveMode;

            return transportTransaction.TryGet(ReceiveWithReceiveOnlyTransaction.ReceiveOnlyTransactionMode, out inReceiveMode);
        }

        SqlConnectionFactory connectionFactory;
        QueueAddressParser addressParser;
    }
}