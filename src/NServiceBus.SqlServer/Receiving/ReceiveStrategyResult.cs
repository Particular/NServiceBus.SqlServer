namespace NServiceBus.Transport.SQLServer
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