namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Extensibility;

    interface IQueueDispatcher
    {
        Task DispatchAsNonIsolated(List<MessageWithAddress> operations, ContextBag context);

        Task DispatchAsIsolated(List<MessageWithAddress> operations);
    }
}