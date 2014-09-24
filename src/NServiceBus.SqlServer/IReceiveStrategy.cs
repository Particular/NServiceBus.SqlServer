namespace NServiceBus.Transports.SQLServer
{
    interface IReceiveStrategy
    {
        ReceiveResult TryReceiveFrom(string tableName);
    }
}