namespace NServiceBus.Transports.SQLServer
{
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