namespace NServiceBus.Transport.Sql.Shared.PubSub
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISubscriptionStore
    {
        Task<List<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default);
        Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken = default);
        Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default);
    }
}