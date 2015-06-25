namespace NServiceBus.Transports.SQLServer
{
    using System.Data.Common;

    class ConnectionStringParser
    {
        public static ConnectionInfo AsConnectionInfo( string connectionString )
        {
            const string key = "Queue Schema";
            string _schemaName = null;
            string _connectionString = connectionString;

            var connectionStringParser = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if( connectionStringParser.ContainsKey( key ) )
            {
                _schemaName = ( string )connectionStringParser[ key ];
                connectionStringParser.Remove( key );
                _connectionString = connectionStringParser.ConnectionString;
            }

            return new ConnectionInfo( _connectionString, _schemaName );
        }
    }
}
