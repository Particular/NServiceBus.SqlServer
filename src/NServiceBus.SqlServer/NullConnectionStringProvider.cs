namespace NServiceBus.Transports.SQLServer
{
    class NullConnectionStringProvider : IConnectionStringProvider
    {
        public string GetForDestination(Address destination)
        {
            return null;
        }
    }
}