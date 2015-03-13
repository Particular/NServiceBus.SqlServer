namespace NServiceBus.Transports.SQLServer
{
    using System;

    /// <summary>
    /// Defines how to connect to a remote SQLServer transport endpoint.
    /// </summary>
    public class EndpointConnectionInfo
    {
        readonly string endpoint;
        readonly string schemaName;
        readonly string connectionString;

        private EndpointConnectionInfo(string endpoint, string connectionString, string schemaName)
        {
            this.endpoint = endpoint;
            this.connectionString = connectionString;
            this.schemaName = schemaName;
        }

        internal string Endpoint
        {
            get { return endpoint; }
        }

        /// <summary>
        /// Creates new instance of <see cref="EndpointConnectionInfo"/> for a given remote endpoint.
        /// </summary>
        /// <param name="endpoint">Name of the endpoint.</param>
        /// <returns></returns>
        public static EndpointConnectionInfo For(string endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException("endpoint");
            }
            return new EndpointConnectionInfo(endpoint, null, null);
        }

        /// <summary>
        /// Instructs the current endpoint to use provided connection string for connecting to the remote endpoint instead of the one configured for the current endpoint.
        /// </summary>
        /// <param name="connectionString">Specific connection string or null for the defualt connection string (same as this endpoint)</param>
        /// <returns></returns>
        public EndpointConnectionInfo UseConnectionString(string connectionString)
        {
            return new EndpointConnectionInfo(Endpoint, connectionString, schemaName);
        }

        /// <summary>
        /// Instructs the current endpoint to use provided schema for connecting to the remote endpoint instead of the one configured for the current endpoint.
        /// </summary>
        /// <param name="schemaName">Specific schema name or null for the default schema (same as this endpoint)</param>
        /// <returns></returns>
        public EndpointConnectionInfo UseSchema(string schemaName)
        {
            return new EndpointConnectionInfo(Endpoint, connectionString, schemaName);
        }

        internal ConnectionParams CreateConnectionParams(LocalConnectionParams defaultConnectionParams)
        {
            return defaultConnectionParams.MakeSpecific(connectionString, schemaName);
        }
    }
}