namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface IQueueDispatcher
    {
        Task DispatchAsNonIsolated(List<UnicastTransportOperation> operations, TransportTransaction transportTransaction);

        Task DispatchAsIsolated(List<UnicastTransportOperation> operations);
    }
}