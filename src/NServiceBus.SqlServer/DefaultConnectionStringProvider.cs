namespace NServiceBus.Transports.SQLServer
{
    class DefaultConnectionStringProvider : IConnectionStringProvider
    {
        readonly LocalConnectionParams localConnectionParams;

        public DefaultConnectionStringProvider(LocalConnectionParams localConnectionParams)
        {
            this.localConnectionParams = localConnectionParams;
        }

        public ConnectionParams GetForDestination(string destination)
        {
            return localConnectionParams;
        }

        public bool AllowsNonLocalConnectionString { get { return false; }}
    }
}