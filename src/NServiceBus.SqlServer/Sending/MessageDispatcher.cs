namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Extensibility;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(SqlConnectionFactory connectionFactory, QueueAddressParser addressParser, SubscriptionReader subscriptionReader)
        {
            this.connectionFactory = connectionFactory;
            this.addressParser = addressParser;
            this.subscriptionReader = subscriptionReader;
            this.random = new Random();
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, ContextBag context)
        {
            foreach (var operation in operations.UnicastTransportOperations)
            {
                await DispatchUnicastOperation(operation.Destination, context, operation).ConfigureAwait(false);
            }

            foreach (var operation in operations.MulticastTransportOperations)
            {
                await DispatchMulticastOperation(context, operation).ConfigureAwait(false);
            }
        }

        async Task DispatchMulticastOperation(ContextBag context, MulticastTransportOperation operation)
        {
            var subscribers = await subscriptionReader.GetSubscribersFor(operation.MessageType).ConfigureAwait(false);
            var subscribersByEndpoint = subscribers.GroupBy(s => s.Endpoint);
            foreach (var endpoint in subscribersByEndpoint)
            {
                var subscriberArray = endpoint.ToArray();
                var randomDestination = subscriberArray[random.Next(subscriberArray.Length)];
                await DispatchUnicastOperation(randomDestination.TransportAddress, context, operation).ConfigureAwait(false);
            }
        }

        async Task DispatchUnicastOperation(string transportAddress, ContextBag context, IOutgoingTransportOperation operation)
        {
            var destination = addressParser.Parse(transportAddress);
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

        async Task DispatchAsSeparateSendOperation(TableBasedQueue queue, IOutgoingTransportOperation operation)
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

        async Task DispatchInCurrentTransportTransaction(TransportTransaction transportTransaction, TableBasedQueue queue, IOutgoingTransportOperation operation)
        {
            SqlConnection connection;
            SqlTransaction transaction;      

            transportTransaction.TryGet(out connection);
            transportTransaction.TryGet(out transaction);

            await queue.SendMessage(operation.Message, connection, transaction).ConfigureAwait(false);
        }

        async Task DispatchInIsolatedTransactionScope(TableBasedQueue queue, IOutgoingTransportOperation operation)
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
        SubscriptionReader subscriptionReader;
        Random random;
    }
}