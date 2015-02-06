namespace NServiceBus.Transports.SQLServer
{
    using System;

    class ConnectionParams
    {
        const string DefaultSchema = "dbo";
        readonly string connectionString;
        readonly string schema;

        public ConnectionParams(string specificConnectionString, string specificSchema, string defaultConnectionString, string defaultSchema)
        {
            if (defaultConnectionString == null)
            {
                throw new ArgumentNullException("defaultConnectionString");
            }

            connectionString = specificConnectionString ?? defaultConnectionString;
            schema = specificSchema ?? defaultSchema ?? DefaultSchema;
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