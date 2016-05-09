namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface IDispatchStrategy
    {
        Task Dispatch(List<MessageWithAddress> operations);
    }
}