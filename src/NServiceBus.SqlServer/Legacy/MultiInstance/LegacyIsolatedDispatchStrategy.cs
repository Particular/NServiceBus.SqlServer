namespace NServiceBus.Transports.SQLServer
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

        LegacySqlConnectionFactory connectionFactory;
    }
}