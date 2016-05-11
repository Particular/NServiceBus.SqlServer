namespace NServiceBus.Transport.SQLServer
{
    using Transports;

    interface IDispatchPolicy
    {
        IDispatchStrategy CreateDispatchStrategy(DispatchConsistency dispatchConsistency);
    }
}