namespace NServiceBus.Transports.SQLServer
{
    class ConnectionInfo
    {
        internal ConnectionInfo( string connectionString, string schemaName )
        {
            ConnectionString = connectionString;
            SchemaName = schemaName;
            if( string.IsNullOrWhiteSpace( SchemaName ) ) 
            {
                SchemaName = "dbo";
            }
        }

        public string SchemaName { get; private set; }

        public string ConnectionString { get; private set; }
    }
}
