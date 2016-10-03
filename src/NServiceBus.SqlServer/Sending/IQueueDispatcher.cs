namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface IQueueDispatcher
    {
        Task DispatchAsNonIsolated(HashSet<MessageWithAddress> operations, TransportTransaction transportTransaction);

        Task DispatchAsIsolated(HashSet<MessageWithAddress> operations);
    }
}