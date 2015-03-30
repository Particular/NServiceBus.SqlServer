namespace NServiceBus.Transports.SQLServer
{
    class NullConnectionStringProvider : IConnectionStringProvider
    {
        public ConnectionParams GetForDestination(string destination)
        {
            return null;
        }

        public bool AllowsNonLocalConnectionString { get { return false; } }
    }
}