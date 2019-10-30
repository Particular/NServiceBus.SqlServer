namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface IManageTransportSubscriptions
    {
        Task<List<string>> GetSubscribersForEvent(Type eventType);
        Task Subscribe(string endpointName, string endpointAddress, Type eventType);
        Task Unsubscribe(string endpointName, Type eventType);
    }
}