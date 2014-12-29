namespace NServiceBus.Transports.SQLServer
{
    interface IConnectionStringProvider
    {
        ConnectionParams GetForDestination(Address destination);
    }
}