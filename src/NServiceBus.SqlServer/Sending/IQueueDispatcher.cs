namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Extensibility;

    interface IQueueDispatcher
    {
        Task DispatchAsNonIsolated(HashSet<MessageWithAddress> operations, ContextBag context);

        Task DispatchAsIsolated(HashSet<MessageWithAddress> operations);
    }
}