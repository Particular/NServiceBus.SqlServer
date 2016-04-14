namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class SqlConnectionFactory
    {
        public SqlConnectionFactory(Func<Task<SqlConnection>> factory)
        {
            openNewConnection = factory;
        }

        public Task<SqlConnection> OpenNewConnection()
        {
            return openNewConnection();
        }

        public static SqlConnectionFactory Default(string connectionString)
        {
            return new SqlConnectionFactory(async () =>
            {
                var connection = new SqlConnection(connectionString);
                try
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    connection.Dispose();
                    throw;
                }

                return connection;
            });
        }

        Func<Task<SqlConnection>> openNewConnection;
    }
}