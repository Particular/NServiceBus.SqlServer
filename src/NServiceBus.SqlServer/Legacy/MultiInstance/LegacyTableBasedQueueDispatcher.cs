namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;

    class LegacyTableBasedQueueDispatcher : IQueueDispatcher
    {
        public LegacyTableBasedQueueDispatcher(LegacySqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public virtual async Task DispatchAsNonIsolated(List<MessageWithAddress> operations, ContextBag context)
        {
            //If dispatch is not isolated then either TS has been created by the receive operation or needs to be created here.
            using (var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
            {
                foreach (var operation in operations)
                {
                    var queue = new TableBasedQueue(operation.Address);
                    using (var connection = await connectionFactory.OpenNewConnection(queue.TransportAddress).ConfigureAwait(false))
                    {
                        await queue.Send(operation.Message, connection, null).ConfigureAwait(false);
                    }
                }
                scope.Complete();
            }
        }

        public virtual async Task DispatchAsIsolated(List<MessageWithAddress> operations)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            {
                foreach (var operation in operations)
                {
                    var queue = new TableBasedQueue(operation.Address);
                    using (var connection = await connectionFactory.OpenNewConnection(queue.TransportAddress).ConfigureAwait(false))
                    {
                        await queue.Send(operation.Message, connection, null).ConfigureAwait(false);
                    }
                }
                scope.Complete();
            }
        }

        LegacySqlConnectionFactory connectionFactory;
    }
}