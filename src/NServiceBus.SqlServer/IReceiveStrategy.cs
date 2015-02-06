namespace NServiceBus.Transports.SQLServer
{
    interface IReceiveStrategy
    {
        ReceiveResult TryReceiveFrom(TableBasedQueue queue);
    }
}