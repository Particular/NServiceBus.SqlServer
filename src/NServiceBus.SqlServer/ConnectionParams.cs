
namespace NServiceBus.Transports.SQLServer
{
    using System;

    class ConnectionParams
    {
        const string DefaultSchema = "dbo";
        public const string DefaultSchemaSettingsKey = "SqlServer.SchemaName";

        public ConnectionParams(string connectionString, string schema)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            ConnectionString = connectionString;
            Schema = schema ?? DefaultSchema;
        }


        public string ConnectionString { get; }

        public string Schema { get; }
    }
}