namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
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
            using (var transaction = BeginTransactionIfNecessary(connection, operations.Count))
            {
                foreach (var operation in operations)
                {
                    var queue = new TableBasedQueue(operation.Address);
                    await queue.Send(operation.Message, connection, transaction).ConfigureAwait(false);
                }
                transaction?.Commit();
            }
        }

        static SqlTransaction BeginTransactionIfNecessary(SqlConnection connection, int count)
        {
            return count == 1 
                ? null //No need for an explicit transaction if we only have one operation
                : connection.BeginTransaction();
        }

        SqlConnectionFactory connectionFactory;
    }
}