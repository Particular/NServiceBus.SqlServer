namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;

    class LegacyIsolatedDispatchStrategy : IDispatchStrategy
    {
        public LegacyIsolatedDispatchStrategy(LegacySqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task Dispatch(List<MessageWithAddress> operations)
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