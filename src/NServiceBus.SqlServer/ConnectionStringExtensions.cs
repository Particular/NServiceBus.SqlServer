namespace NServiceBus.Features
{
    using System.Data.Common;

    static class ConnectionStringExtensions
    {
        public static string ExtractSchemaName(this string connectionString, out string schemaName)
        {
            const string key = "Queue Schema";

            var connectionStringParser = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };
            if (connectionStringParser.ContainsKey(key))
            {
                schemaName = (string) connectionStringParser[key];
                connectionStringParser.Remove(key);
                connectionString = connectionStringParser.ConnectionString;
            }
            else
            {
                schemaName = null;
            }
            return connectionString;
        }
    }
}