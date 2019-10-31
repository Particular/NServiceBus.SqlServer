namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;

    class SubscriptionManager : IManageSubscriptions
    {
        public SubscriptionManager(ISubscriptionStore subscriptionStore, Func<Type, IEnumerable<Type>> typeHierarchyResolver, string endpointName, string localAddress)
        {
            this.subscriptionStore = subscriptionStore;
            this.typeHierarchyResolver = typeHierarchyResolver;
            this.endpointName = endpointName;
            this.localAddress = localAddress;
        }

        public Task Subscribe(Type eventType, ContextBag context)
        {
            return InvokeForEachTypeInHierarchy(eventType, topic => subscriptionStore.Subscribe(endpointName, localAddress, topic));
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            return InvokeForEachTypeInHierarchy(eventType, topic => subscriptionStore.Unsubscribe(endpointName, topic));
        }

        Task InvokeForEachTypeInHierarchy(Type type, Func<string, Task> action)
        {
            var allTasks = typeHierarchyResolver(type).Select(x => action(TopicName(x))).ToArray();
            return Task.WhenAll(allTasks);
        }

        static string TopicName(Type type)
        {
            return $"{type.Namespace}.{type.Name}";
        }

        ISubscriptionStore subscriptionStore;
        Func<Type, IEnumerable<Type>> typeHierarchyResolver;
        string endpointName;
        string localAddress;
    }
}