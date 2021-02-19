using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Extensibility;
using NServiceBus.Features;
using NServiceBus.Persistence;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

public class TestingInMemoryPersistence : PersistenceDefinition
{
    internal TestingInMemoryPersistence()
    {
        Supports<StorageType.Subscriptions>(s =>
        {
            s.EnableFeatureByDefault<TestingInMemorySubscriptionPersistence>();
        });
    }
}

public static class InMemoryPersistenceExtensions
{
    public static void UseStorage(this PersistenceExtensions<TestingInMemoryPersistence> extensions, TestingInMemorySubscriptionStorage storageInstance)
    {
        extensions.GetSettings().Set("InMemoryPersistence.StorageInstance", storageInstance);
    }
}

public class TestingInMemorySubscriptionPersistence : Feature
{
    internal TestingInMemorySubscriptionPersistence()
    {
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        var storageInstance = context.Settings.GetOrDefault<TestingInMemorySubscriptionStorage>("InMemoryPersistence.StorageInstance");
        context.Services.AddSingleton<ISubscriptionStorage>(storageInstance ?? new TestingInMemorySubscriptionStorage());
    }
}

public class TestingInMemorySubscriptionStorage : ISubscriptionStorage
{
    public Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken = default)
    {
        var dict = storage.GetOrAdd(messageType, type => new ConcurrentDictionary<string, Subscriber>(StringComparer.OrdinalIgnoreCase));

        dict.AddOrUpdate(BuildKey(subscriber), _ => subscriber, (_, __) => subscriber);
        return Task.FromResult(true);
    }

    static string BuildKey(Subscriber subscriber)
    {
        return $"{subscriber.TransportAddress ?? ""}_{subscriber.Endpoint ?? ""}";
    }

    public Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken = default)
    {
        if (storage.TryGetValue(messageType, out var dict))
        {
            dict.TryRemove(BuildKey(subscriber), out var _);
        }
        return Task.FromResult(true);
    }

    public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context, CancellationToken cancellationToken = default)
    {
        var result = new HashSet<Subscriber>();
        foreach (var m in messageTypes)
        {
            if (storage.TryGetValue(m, out var list))
            {
                result.UnionWith(list.Values);
            }
        }
        return Task.FromResult((IEnumerable<Subscriber>)result);
    }

    ConcurrentDictionary<MessageType, ConcurrentDictionary<string, Subscriber>> storage = new ConcurrentDictionary<MessageType, ConcurrentDictionary<string, Subscriber>>();
}
