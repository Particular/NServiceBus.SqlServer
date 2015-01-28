namespace NServiceBus.Transports.SQLServer
{
    class DefaultConnectionStringProvider : IConnectionStringProvider
    {
        readonly LocalConnectionParams localConnectionParams;

        public DefaultConnectionStringProvider(LocalConnectionParams localConnectionParams)
        {
            this.localConnectionParams = localConnectionParams;
        }

        public ConnectionParams GetForDestination(Address destination)
        {
            return localConnectionParams;
        }
    }
}