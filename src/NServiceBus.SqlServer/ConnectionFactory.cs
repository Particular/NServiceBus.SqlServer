namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;

    class ConnectionFactory
    {
        readonly Func<string, SqlConnection> openNewConnection;

        public ConnectionFactory(Func<string, SqlConnection> factory)
        {
            openNewConnection = factory;
        }

        public SqlConnection OpenNewConnection(string connectionString)
        {
            return openNewConnection(connectionString);
        }

        public static ConnectionFactory Default()
        {
            return new ConnectionFactory(cs =>
            {
                var connection = new SqlConnection(cs);
                try
                {
                    connection.Open();
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
