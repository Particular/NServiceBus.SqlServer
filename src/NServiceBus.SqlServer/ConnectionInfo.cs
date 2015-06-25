namespace NServiceBus.Transports.SQLServer
{
    class ConnectionInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionInfo"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schemaName">Name of the schema.</param>
        internal ConnectionInfo( string connectionString, string schemaName )
        {
            this.ConnectionString = connectionString;
            this.SchemaName = schemaName;
            if( string.IsNullOrWhiteSpace( this.SchemaName ) ) 
            {
                this.SchemaName = "dbo";
            }
        }

        /// <summary>
        /// Gets the name of the schema.
        /// </summary>
        /// <value>
        /// The name of the schema.
        /// </value>
        public string SchemaName { get; private set; }

        /// <summary>
        /// Gets the connection string.
        /// </summary>
        /// <value>
        /// The connection string.
        /// </value>
        public string ConnectionString { get; private set; }
    }
}
