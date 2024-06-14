namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    interface ISubscriptionStore
    {
        Task<List<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default);
        Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken = default);
        Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default);
    }
}