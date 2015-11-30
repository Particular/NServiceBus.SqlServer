namespace NServiceBus.Transports.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    // Should we make this async in core?
    class SqlServerQueueCreator : ICreateQueues
    {
        ConnectionParams connectionParams;

        public SqlServerQueueCreator(ConnectionParams connectionParams)
        {
            this.connectionParams = connectionParams;
        }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            foreach (var receivingAddress in queueBindings.ReceivingAddresses)
            {
                CreateQueueIfNecessary(receivingAddress, identity);
            }

            foreach (var sendingAddress in queueBindings.SendingAddresses)
            {
                CreateQueueIfNecessary(sendingAddress, identity);
            }

            return TaskEx.Completed;
        }

        void CreateQueueIfNecessary(string address, string account)
        {
            using (var connection = new SqlConnection(connectionParams.ConnectionString))
            {
                connection.Open();

                var sql = string.Format(Sql.CreateQueueText, connectionParams.Schema, address);
                using (var transaction = connection.BeginTransaction())
                using (var command = new SqlCommand(sql, connection, transaction) { CommandType = CommandType.Text })
                {
                    command.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }
    }
}