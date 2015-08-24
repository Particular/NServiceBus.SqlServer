namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;

    class ConnectionFactory
    {
        public readonly Func<string, SqlConnection> OpenNewConnection;

        public ConnectionFactory(Func<string, SqlConnection> factory)
        {
            OpenNewConnection = factory;
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
