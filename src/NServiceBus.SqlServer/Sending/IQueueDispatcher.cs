namespace NServiceBus.Transport.SQLServer
{
    interface IQueueDispatcher
    {
        IDispatchStrategy CreateIsolatedDispatchStrategy();
        IDispatchStrategy CreateNonIsolatedDispatchStrategy();
    }
}