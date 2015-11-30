namespace NServiceBus.Transports.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class SqlServerQueueCreator : ICreateQueues
    {
        ConnectionParams connectionParams;

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            using (var connection = new SqlConnection(connectionParams.ConnectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var receivingAddress in queueBindings.ReceivingAddresses)
                    {
                        await CreateQueue(receivingAddress, connection, transaction);
                    }
                    foreach (var receivingAddress in queueBindings.SendingAddresses)
                    {
                        await CreateQueue(receivingAddress, connection, transaction);
                    }
                    transaction.Commit();
                }
            }
        }

        async Task CreateQueue(string address, SqlConnection connection, SqlTransaction transaction)
        {
            var sql = string.Format(Sql.CreateQueueText, connectionParams.Schema, address);
            using (var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        public SqlServerQueueCreator(ConnectionParams connectionParams)
        {
            this.connectionParams = connectionParams;
        }
    }
}