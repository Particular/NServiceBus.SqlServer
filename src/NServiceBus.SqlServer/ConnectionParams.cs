
namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.Common;

    class ConnectionParams
    {
        const string DefaultSchema = "dbo";
        string connectionString;
        string schema;

        //TODO: when adding support for multip-db setup provide more params to connectionParams
        //i.e. values read from config file and from code config
        //TODO: figure out what context.ConnectionString/connectionStringWithSchema is (value from code/config?)
        //does it have consistent precedence rules with http://docs.particular.net/nservicebus/sqlserver/multiple-databases#current-endpoint ?
        public ConnectionParams(string connectionStringWithSchema)
        {
            if (connectionStringWithSchema == null)
            {
                throw new ArgumentNullException("connectionString");
            }

            string schemaName;
            connectionString = TryExtractSchemaName(connectionStringWithSchema, out schemaName);
            schema = schemaName ?? DefaultSchema;
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

        public string ConnectionString
        {
            get { return connectionString; }
        }

        public string Schema
        {
            get { return schema; }
        }
    }
}
