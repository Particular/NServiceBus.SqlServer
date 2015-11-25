namespace NServiceBus.Transports.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;

    // Should we make this async in core?
    class SqlServerQueueCreator : ICreateQueues
    {
        readonly ConnectionParams connectionParams;

        public void CreateQueueIfNecessary(string address, string account)
        {
            using (var connection = new SqlConnection(connectionParams.ConnectionString))
            {
                connection.Open();

                var sql = string.Format(Sql.CreateQueueText, connectionParams.Schema, address);
                using (var transaction = connection.BeginTransaction())
                using (var command = new SqlCommand(sql, connection, transaction) {CommandType = CommandType.Text})
                {
                    command.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }

        public SqlServerQueueCreator(ConnectionParams connectionParams)
        {
            this.connectionParams = connectionParams;
        }
    }
}