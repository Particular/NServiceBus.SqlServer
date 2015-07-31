namespace NServiceBus.Transports.SQLServer
{
    using System.Data.SqlClient;

    /// <summary>
    /// Factory of SQL connections.
    /// </summary>
    public interface ISqlConnectionFactory
    {
        /// <summary>
        /// Creates new SqlConnection instance and opens it.
        /// </summary>
        SqlConnection OpenNewConnection(string connectionString);
    }
}