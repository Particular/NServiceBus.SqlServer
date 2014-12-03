namespace NServiceBus.Transports.SQLServer
{
    class DefaultConnectionStringProvider : IConnectionStringProvider
    {
        readonly string defaultConnectionString;

        public DefaultConnectionStringProvider(string defaultConnectionString)
        {
            this.defaultConnectionString = defaultConnectionString;
        }

        public string GetForDestination(Address destination)
        {
            return defaultConnectionString;
        }
    }
}