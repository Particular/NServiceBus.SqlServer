namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class ReceiveConnectionDispatchStrategy : IDispatchStrategy
    {
        
        public ReceiveConnectionDispatchStrategy(SqlConnection connection, SqlTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public async Task Dispatch(List<MessageWithAddress> operations)
        {
            foreach (var operation in operations)
            {
                var queue = new TableBasedQueue(operation.Address);

                await queue.Send(operation.Message, connection, transaction).ConfigureAwait(false);
            }
        }

        SqlConnection connection;
        SqlTransaction transaction;
    }
}