namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Extensibility;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(SqlConnectionFactory connectionFactory, QueueAddressProvider addressProvider)
        {
            this.connectionFactory = connectionFactory;
            this.addressProvider = addressProvider;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, ContextBag context)
        {
            foreach (var operation in operations.UnicastTransportOperations)
            {
                var destination = addressProvider.Parse(operation.Destination);
                var queue = new TableBasedQueue(destination);

                //Dispatch in separate transaction even if transaction scope already exists
                if (operation.RequiredDispatchConsistency == DispatchConsistency.Isolated)
                {
                    await DispatchInIsolatedTransactionScope(queue, operation);
                }

                ReceiveContext receiveContext;
                if (context.TryGet(out receiveContext))
                {
                    await DispatchInCurrentReceiveContext(receiveContext, queue, operation);
                }
                else
                {
                    await DispatchAsSeparateSendOperation(queue, operation);
                }
            }
        }

        async Task DispatchAsSeparateSendOperation(TableBasedQueue queue, UnicastTransportOperation operation)
        {
            using (var connection = await connectionFactory.OpenNewConnection())
            {
                using (var transaction = connection.BeginTransaction())
                {
                    await queue.SendMessage(operation.Message, connection, transaction).ConfigureAwait(false);

                    transaction.Commit();
                }
            }
        }

        async Task DispatchInCurrentReceiveContext(ReceiveContext receiveContext, TableBasedQueue queue, UnicastTransportOperation operation)
        {
            SqlConnection connection;
            SqlTransaction transaction;

            GetSqlResources(receiveContext, out connection, out transaction);

            await queue.SendMessage(operation.Message, connection, transaction).ConfigureAwait(false);
        }

        async Task DispatchInIsolatedTransactionScope(TableBasedQueue queue, UnicastTransportOperation operation)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection())
                {
                    await queue.SendMessage(operation.Message, connection, null).ConfigureAwait(false);
                }

                scope.Complete();
            }
        }

        void GetSqlResources(ReceiveContext receiveContext, out SqlConnection connection, out SqlTransaction transaction)
        {
            switch (receiveContext.Type)
            {
                case ReceiveType.TransactionScope:
                    connection = receiveContext.Connection;
                    transaction = null;
                    break;
                case ReceiveType.NativeTransaction:
                    connection = receiveContext.Transaction.Connection;
                    transaction = receiveContext.Transaction;
                    break;
                case ReceiveType.NoTransaction:
                    connection = receiveContext.Connection;
                    transaction = null;
                    break;
                default:
                    throw new Exception("Invalid receive type");
            }
        }

        SqlConnectionFactory connectionFactory;
        QueueAddressProvider addressProvider;
    }
}