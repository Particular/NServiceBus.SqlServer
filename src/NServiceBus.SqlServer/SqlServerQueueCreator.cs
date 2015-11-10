namespace NServiceBus.Transports.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;

    class SqlServerQueueCreator : ICreateQueues
    {
        readonly ConnectionParams connectionParams;

        public void CreateQueueIfNecessary(string address, string account)
        {
            using (var connection = new SqlConnection(connectionParams.ConnectionString))
            {
                connection.Open();

                var sql = string.Format(Sql.CreateQueueText, connectionParams.Schema, address);

                using (var command = new SqlCommand(sql, connection) {CommandType = CommandType.Text})
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public SqlServerQueueCreator(ConnectionParams connectionParams)
        {
            this.connectionParams = connectionParams;
        }
    }
}