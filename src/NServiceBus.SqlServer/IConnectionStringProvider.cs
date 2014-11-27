namespace NServiceBus.Transports.SQLServer
{
    interface IConnectionStringProvider
    {
        string GetForDestination(Address destination);
    }
}