namespace NServiceBus.Transports.SQLServer
{
    class DefaultConnectionStringProvider : IConnectionStringProvider
    {
        readonly ConnectionParams defaultConnectionInfo;

        public DefaultConnectionStringProvider(ConnectionParams defaultConnectionInfo)
        {
            this.defaultConnectionInfo = defaultConnectionInfo;
        }

        public ConnectionParams GetForDestination(Address destination)
        {
            return defaultConnectionInfo;
        }
    }
}