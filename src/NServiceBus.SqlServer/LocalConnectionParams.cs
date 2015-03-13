namespace NServiceBus.Transports.SQLServer
{
    class LocalConnectionParams : ConnectionParams
    {

        public LocalConnectionParams(string specificSchema, string defaultConnectionString, string defaultSchema)
            : base(null, specificSchema, defaultConnectionString, defaultSchema)
        {
        }

        public ConnectionParams MakeSpecific(string specificConnectionString, string specificSchema)
        {
            return new ConnectionParams(specificConnectionString, specificSchema, ConnectionString, Schema);
        }
    }
}