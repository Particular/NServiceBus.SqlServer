namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;

    /// <summary>
    /// Default implementation of SQL connections factory.
    /// </summary>
    public class DefaultSqlConnectionFactory : ISqlConnectionFactory
    {
        /// <summary>
        /// Creates new SqlConnection instances and opens connections.
        /// </summary>
        public SqlConnection OpenNewConnection(string connectionString)
        {
            var connection = new SqlConnection(connectionString);

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
        }
    }
}