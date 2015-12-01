namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;

    class SqlServerMessageDispatcher : IDispatchMessages
    {
        string connectionString;
        SqlServerAddressProvider addressProvider;

        public SqlServerMessageDispatcher(string connectionString, SqlServerAddressProvider addressProvider)
        {
            this.connectionString = connectionString;
            this.addressProvider = addressProvider;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(IEnumerable<TransportOperation> transportOperations, ContextBag context)
        {
            foreach (var operation in transportOperations)
            {
                var dispatchOptions = operation.DispatchOptions;
                var routingStrategy = dispatchOptions.AddressTag as UnicastAddressTag;

                if (routingStrategy == null)
                {
                    throw new Exception("The Sql transport only supports the `DirectRoutingStrategy`, strategy required " + dispatchOptions.AddressTag.GetType().Name);
                }

                var destination = addressProvider.Parse(routingStrategy.Destination);
                var queue = new TableBasedQueue(destination);

                //Dispatch in separate transaction even if transaction scope already exists
                if (dispatchOptions.RequiredDispatchConsistency == DispatchConsistency.Isolated)
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

        async Task DispatchAsSeparateSendOperation(TableBasedQueue queue, TransportOperation operation)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                using (var transaction = connection.BeginTransaction())
                {
                    await queue.SendMessage(operation.Message, connection, transaction).ConfigureAwait(false);

                    transaction.Commit();
                }
            }
        }

        async Task DispatchInCurrentReceiveContext(ReceiveContext receiveContext, TableBasedQueue queue, TransportOperation operation)
        {
            SqlConnection connection;
            SqlTransaction transaction;

            GetSqlResources(receiveContext, out connection, out transaction);

            await queue.SendMessage(operation.Message, connection, transaction).ConfigureAwait(false);
        }

        async Task DispatchInIsolatedTransactionScope(TableBasedQueue queue, TransportOperation operation)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync().ConfigureAwait(false);

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
    }
}