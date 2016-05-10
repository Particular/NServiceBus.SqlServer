namespace NServiceBus.Transports.SQLServer
{
    enum ReceiveStrategyResult
    {
        PoisonMessage,
        NoMessage,
        Success,
        ProcessingAborted,
        Error
    }
}