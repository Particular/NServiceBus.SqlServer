namespace NServiceBus.Transports.SQLServer
{
    using System.Data.Common;

    /// <summary>
    /// Connection string utilities.
    /// </summary>
    public class ConnectionStringParser
    {
        /// <summary>
        /// Parses the supplied connection string extracting the schema name, if any.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <returns></returns>
        public static ConnectionInfo AsConnectionInfo( string connectionString )
        {
            const string key = "Queue Schema";
            string _schemaName = null;
            var _connectionString = connectionString;

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
