namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class SqlConnectionFactory
    {
        readonly string connectionString;
        readonly Func<string, Task<SqlConnection>> openNewConnection;

        public SqlConnectionFactory(string connectionString, Func<string, Task<SqlConnection>> factory)
        {
            this.connectionString = connectionString;
            openNewConnection = factory;
        }

        public async Task<SqlConnection> OpenNewConnection()
        {
            return await openNewConnection(connectionString);
        }

        public static SqlConnectionFactory Default(string connectionString)
        {
            return new SqlConnectionFactory(connectionString, async cs =>
            {
                var connection = new SqlConnection(cs);
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