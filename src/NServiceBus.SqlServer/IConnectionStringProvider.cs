namespace NServiceBus.Transports.SQLServer
{
    interface IConnectionStringProvider
    {
        ConnectionParams GetForDestination(string destination);
        bool AllowsNonLocalConnectionString { get; }
    }
}