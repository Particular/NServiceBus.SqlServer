namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class SeparateConnectionDispatchStrategy : IDispatchStrategy
    {
        public SeparateConnectionDispatchStrategy(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task Dispatch(List<MessageWithAddress> operations)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var operation in operations)
                {
                    var queue = new TableBasedQueue(operation.Address);
                    await queue.Send(operation.Message, connection, transaction).ConfigureAwait(false);
                }
                transaction.Commit();
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}