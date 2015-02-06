namespace NServiceBus.Transports.SQLServer
{
    class NullConnectionStringProvider : IConnectionStringProvider
    {
        public ConnectionParams GetForDestination(Address destination)
        {
            return null;
        }
    }
}