namespace NServiceBus.Transports.SQLServer
{
    interface IReceiveStrategy
    {
        ReceiveResult TryReceive(string sql);
    }
}