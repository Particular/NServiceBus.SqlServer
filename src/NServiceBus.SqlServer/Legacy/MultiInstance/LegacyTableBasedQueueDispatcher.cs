namespace NServiceBus.Transports.SQLServer.Legacy.MultiInstance
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;

    class LegacyTableBasedQueueDispatcher
    {
        public LegacyTableBasedQueueDispatcher(LegacySqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public virtual Task DispatchAsNonIsolated(List<MessageWithAddress> defaultConsistencyOperations)
        {
            return Task.WhenAll(GetDispatchTasks(defaultConsistencyOperations));
        }

        IEnumerable<Task> GetDispatchTasks(List<MessageWithAddress> defaultConsistencyOperations)
        {
            foreach (var operation in defaultConsistencyOperations)
            {
                yield return GetDispatchTask(operation);
            }
        }

        async Task GetDispatchTask(MessageWithAddress operation)
        {
            var queue = new TableBasedQueue(operation.Address);

            //If dispatch is not isolated then transaction scope has already been created by <see cref="LegacyReceiveWithTransactionScope"/>
            using (var connection = await connectionFactory.OpenNewConnection(queue.TransportAddress).ConfigureAwait(false))
            {
                await queue.SendMessage(operation.Message, connection, null).ConfigureAwait(false);
            }
        }

        public virtual async Task DispatchAsIsolated(List<MessageWithAddress> isolatedConsistencyOperations)
        {
            foreach (var operation in isolatedConsistencyOperations)
            {
                var queue = new TableBasedQueue(operation.Address);

                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
                using (var connection = await connectionFactory.OpenNewConnection(queue.TransportAddress).ConfigureAwait(false))
                {
                    await queue.SendMessage(operation.Message, connection, null).ConfigureAwait(false);

                    scope.Complete();
                }
            }
        }

        LegacySqlConnectionFactory connectionFactory;
    }
}