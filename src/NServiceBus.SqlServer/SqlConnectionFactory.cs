namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class SqlConnectionFactory
    {
        readonly Func<Task<SqlConnection>> openNewConnection;

        public SqlConnectionFactory(Func<Task<SqlConnection>> factory)
        {
            openNewConnection = factory;
        }

        public async Task<SqlConnection> OpenNewConnection()
        {
            return await openNewConnection();
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
    }
}