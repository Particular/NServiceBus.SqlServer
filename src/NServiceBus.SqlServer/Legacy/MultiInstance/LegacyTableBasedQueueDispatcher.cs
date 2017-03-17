namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;

    class LegacyTableBasedQueueDispatcher : IQueueDispatcher
    {
        public LegacyTableBasedQueueDispatcher(LegacySqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public virtual async Task DispatchAsNonIsolated(HashSet<MessageWithAddress> operations, TransportTransaction transportTransaction)
        {
            //If dispatch is not isolated then either TS has been created by the receive operation or needs to be created here.
            using (var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
            {
                foreach (var operation in operations)
                {
                    var queue = queueFactory.Get(operation.Address);
                    using (var connection = await connectionFactory.OpenNewConnection(queue.TransportAddress).ConfigureAwait(false))
                    {
                        await queue.Send(operation.Message, connection, null).ConfigureAwait(false);
                    }
                }
                scope.Complete();
            }
        }

        public virtual async Task DispatchAsIsolated(HashSet<MessageWithAddress> operations)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            {
                foreach (var operation in operations)
                {
                    var queue = queueFactory.Get(operation.Address);
                    using (var connection = await connectionFactory.OpenNewConnection(queue.TransportAddress).ConfigureAwait(false))
                    {
                        await queue.Send(operation.Message, connection, null).ConfigureAwait(false);
                    }
                }
                scope.Complete();
            }
        }

        TableBasedQueueFactory queueFactory = new TableBasedQueueFactory();
        LegacySqlConnectionFactory connectionFactory;
    }
}