namespace NServiceBus.Transports.SQLServer.Legacy.MultiInstance
{
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Extensibility;

    class LegacyMessageDispatcher : IDispatchMessages
    {
        public LegacyMessageDispatcher(LegacySqlConnectionFactory connectionFactory, QueueAddressParser addressParser)
        {
            this.connectionFactory = connectionFactory;
            this.addressParser = addressParser;
        }

        public async Task Dispatch(TransportOperations outgoingMessages, ContextBag context)
        {
            foreach (var operation in outgoingMessages.UnicastTransportOperations)
            {
                await DispatchUnicastOperation(operation).ConfigureAwait(false);
            }
        }

        async Task DispatchUnicastOperation(UnicastTransportOperation operation)
        {
            var destination = addressParser.Parse(operation.Destination);
            var queue = new TableBasedQueue(destination);

            //Dispatch in separate scope to make sure transaction does not get enlisted
            if (operation.RequiredDispatchConsistency == DispatchConsistency.Isolated)
            {
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
                {
                    using (var connection = await connectionFactory.OpenNewConnection(queue.TransportAddress).ConfigureAwait(false))
                    {
                        await queue.SendMessage(operation.Message, connection, null).ConfigureAwait(false);
                    }

                    scope.Complete();
                }

                return;
            }

            //If dispatch is not isolated then transaction scope has already been created by <see cref="LegacyReceiveWithTransactionScope"/>
            using (var connection = await connectionFactory.OpenNewConnection(queue.TransportAddress).ConfigureAwait(false))
            {
                await queue.SendMessage(operation.Message, connection, null).ConfigureAwait(false);
            }
        }

        LegacySqlConnectionFactory connectionFactory;
        QueueAddressParser addressParser;
    }
}