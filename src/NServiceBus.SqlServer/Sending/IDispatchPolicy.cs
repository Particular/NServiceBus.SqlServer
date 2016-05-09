namespace NServiceBus.Transports.SQLServer
{
    interface IDispatchPolicy
    {
        IDispatchStrategy CreateDispatchStrategy(DispatchConsistency dispatchConsistency);
    }
}