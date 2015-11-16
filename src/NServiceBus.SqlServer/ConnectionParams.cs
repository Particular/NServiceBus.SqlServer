
namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.Common;

    class ConnectionParams
    {
        const string DefaultSchema = "dbo";
        public const string DefaultSchemaSettingsKey = "SqlServer.SchemaName";


        //TODO: when adding support for multip-db setup provide more params to connectionParams        
        //for more info see UseSpecificConnectionInformation in v2
        public ConnectionParams(string connectionStringWithSchema, string endpointSpecificSchema)
        {
            if (connectionStringWithSchema == null)
            {
                throw new ArgumentNullException(nameof(connectionStringWithSchema));
            }

            string schemaName;
            ConnectionString = TryExtractSchemaName(connectionStringWithSchema, out schemaName);
            Schema = schemaName ?? endpointSpecificSchema ?? DefaultSchema;

        }

        private static string TryExtractSchemaName(string connectionStringWithSchema, out string schemaName)
        {
            const string key = "Queue Schema";

            var connectionStringParser = new DbConnectionStringBuilder
            {
                ConnectionString = connectionStringWithSchema
            };
            if (connectionStringParser.ContainsKey(key))
            {
                schemaName = (string)connectionStringParser[key];
                connectionStringParser.Remove(key);
                connectionStringWithSchema = connectionStringParser.ConnectionString;
            }
            else
            {
                schemaName = null;
            }
            return connectionStringWithSchema;
        }

        public string ConnectionString { get; }

        public string Schema { get; }
    }
}
