namespace NServiceBus.Transport.PostgreSql;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sql.Shared.PubSub;
using SqlServer;

class SubscriptionStore : ISubscriptionStore
{
    public Task<List<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task Subscribe(string endpointName, string endpointAddress, Type eventType,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}