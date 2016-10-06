namespace NServiceBus.Transport.SQLServer
{
    using System.Threading.Tasks;

    interface IQueueDispatcher
    {
        Task DispatchAsNonIsolated(UnicastTransportOperation[] operations, TransportTransaction transportTransaction);

        Task DispatchAsIsolated(UnicastTransportOperation[] operations);
    }
}