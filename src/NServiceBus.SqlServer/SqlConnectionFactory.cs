namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;

    class SqlConnectionFactory
    {
        readonly string connectionString;
        readonly Func<string, SqlConnection> openNewConnection;

        public SqlConnectionFactory(string connectionString, Func<string, SqlConnection> factory)
        {
            this.connectionString = connectionString;
            openNewConnection = factory;
        }

        public SqlConnection OpenNewConnection()
        {
            return openNewConnection(connectionString);
        }

        public static SqlConnectionFactory Default(string connectionString)
        {
            return new SqlConnectionFactory(connectionString, cs =>
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