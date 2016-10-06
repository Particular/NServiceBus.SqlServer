namespace NServiceBus.Transport.SQLServer
{
    using System.Threading.Tasks;
    using System.Transactions;

    class LegacyTableBasedQueueDispatcher : IQueueDispatcher
    {
        public LegacyTableBasedQueueDispatcher(LegacySqlConnectionFactory connectionFactory, QueueAddressParser addressParser)
        {
            this.connectionFactory = connectionFactory;
            this.addressParser = addressParser;
        }

        public virtual async Task DispatchAsNonIsolated(UnicastTransportOperation[] operations, TransportTransaction transportTransaction)
        {
            //If dispatch is not isolated then either TS has been created by the receive operation or needs to be created here.
            using (var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
            {
                foreach (var operation in operations)
                {
                    var queueAddress = addressParser.Parse(operation.Destination);
                    var queue = new TableBasedQueue(queueAddress);
                    using (var connection = await connectionFactory.OpenNewConnection(queue.TransportAddress).ConfigureAwait(false))
                    {
                        await queue.Send(operation.Message, connection, null).ConfigureAwait(false);
                    }
                }
                scope.Complete();
            }
        }

        public virtual async Task DispatchAsIsolated(UnicastTransportOperation[] operations)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            {
                foreach (var operation in operations)
                {
                    var queueAddress = addressParser.Parse(operation.Destination);
                    var queue = new TableBasedQueue(queueAddress);
                    using (var connection = await connectionFactory.OpenNewConnection(queue.TransportAddress).ConfigureAwait(false))
                    {
                        await queue.Send(operation.Message, connection, null).ConfigureAwait(false);
                    }
                }
                scope.Complete();
            }
        }

        LegacySqlConnectionFactory connectionFactory;
        QueueAddressParser addressParser;
    }
}