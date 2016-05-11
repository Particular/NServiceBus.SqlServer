namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;

    class IsolatedDispatchSendStrategy : IDispatchStrategy
    {
        public IsolatedDispatchSendStrategy(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task Dispatch(List<MessageWithAddress> operations)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                foreach (var operation in operations)
                {
                    var queue = new TableBasedQueue(operation.Address);

                    await queue.Send(operation.Message, connection, null).ConfigureAwait(false);
                }
                scope.Complete();
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}