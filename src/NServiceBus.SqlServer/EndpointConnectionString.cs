namespace NServiceBus.Transports.SQLServer
{
    using System;

    /// <summary>
    /// Represents a binding between an endpoint name and a connection string to be used to connect to this endpoint's queue table.
    /// </summary>
    public class EndpointConnectionString
    {
        readonly string endpointName;
        readonly string connectionString;

        /// <summary>
        /// Creates new instance of <see cref="EndpointConnectionString"/>
        /// </summary>
        /// <param name="endpointName">Name of the endpoint</param>
        /// <param name="connectionString">The connection string to be used to connect to this endpoint's queue table.</param>
        public EndpointConnectionString(string endpointName, string connectionString)
        {
            if (endpointName == null)
            {
                throw new ArgumentNullException("endpointName");
            }
            if (string.IsNullOrEmpty(endpointName))
            {
                throw new ArgumentException("Endpoint name cannot be empty string","endpointName");   
            }
            if (connectionString == null)
            {
                throw new ArgumentNullException("connectionString");
            }
            if (string.IsNullOrEmpty(endpointName))
            {
                throw new ArgumentException("Connection string cannot be empty string", "connectionString");
            }
            this.endpointName = endpointName;
            this.connectionString = connectionString;
        }

        /// <summary>
        /// Returns the name of the endpoint
        /// </summary>
        public string EndpointName
        {
            get { return endpointName; }
        }

        /// <summary>
        /// Returns the connection string to be used for the endpoint.
        /// </summary>
        public string ConnectionString
        {
            get { return connectionString; }
        }
    }
}