namespace NServiceBus.Transports.SQLServer
{
    /// <summary>
    /// Defines how to connect to a remote SQLServer transport endpoint.
    /// </summary>
    public class ConnectionInfo
    {
        readonly string schemaName;
        readonly string connectionString;

        internal ConnectionInfo(string connectionString, string schemaName)
        {
            this.connectionString = connectionString;
            this.schemaName = schemaName;
        }

        /// <summary>
        /// Creates new instance of <see cref="ConnectionInfo"/>.
        /// </summary>
        /// <returns></returns>
        public static ConnectionInfo Create()
        {
            return new ConnectionInfo(null, null);
        }

        /// <summary>
        /// Instructs the current endpoint to use provided connection string for connecting to the remote endpoint instead of the one configured for the current endpoint.
        /// </summary>
        /// <param name="connectionString">Specific connection string or null for the defualt connection string (same as this endpoint)</param>
        /// <returns></returns>
        public ConnectionInfo UseConnectionString(string connectionString)
        {
            return new ConnectionInfo(connectionString, schemaName);
        }

        /// <summary>
        /// Instructs the current endpoint to use provided schema for connecting to the remote endpoint instead of the one configured for the current endpoint.
        /// </summary>
        /// <param name="schemaName">Specific schema name or null for the default schema (same as this endpoint)</param>
        /// <returns></returns>
        public ConnectionInfo UseSchema(string schemaName)
        {
            return new ConnectionInfo(connectionString, schemaName);
        }

        internal ConnectionParams CreateConnectionParams(LocalConnectionParams localConnectionParams)
        {
            return localConnectionParams.MakeSpecific(connectionString, schemaName);
        }
    }
}