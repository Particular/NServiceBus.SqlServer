namespace NServiceBus.Transports.SQLServer
{
    using System;

    class ConnectionParams
    {
        const string DefaultSchema = "dbo";
        readonly string connectionString;
        readonly string schema;

        protected ConnectionParams(string specificSchema, string defaultConnectionString, string defaultSchema)
            : this(null, specificSchema, defaultConnectionString, defaultSchema)
        {
            
        }

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

    class LocalConnectionParams : ConnectionParams
    {
        readonly int primaryPollInterval;
        readonly int secondaryPollInterval;

        public LocalConnectionParams(string specificSchema, string defaultConnectionString, string defaultSchema, int primaryPollInterval, int secondaryPollInterval) 
            : base(specificSchema, defaultConnectionString, defaultSchema)
        {
            this.primaryPollInterval = primaryPollInterval;
            this.secondaryPollInterval = secondaryPollInterval;
        }

        public int PrimaryPollInterval
        {
            get { return primaryPollInterval; }
        }

        public int SecondaryPollInterval
        {
            get { return secondaryPollInterval; }
        }

        public ConnectionParams MakeSpecific(string specificConnectionString, string specificSchema)
        {
            return new ConnectionParams(specificConnectionString, specificSchema, ConnectionString, Schema);
        }
    }
}