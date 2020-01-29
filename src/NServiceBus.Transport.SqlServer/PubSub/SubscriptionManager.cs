namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;

    class SubscriptionManager : IManageSubscriptions
    {
        public SubscriptionManager(ISubscriptionStore subscriptionStore, string endpointName, string localAddress)
        {
            this.subscriptionStore = subscriptionStore;
            this.endpointName = endpointName;
            this.localAddress = localAddress;
        }

        public Task Subscribe(Type eventType, ContextBag context)
        {
            return subscriptionStore.Subscribe(endpointName, localAddress, eventType);
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            return subscriptionStore.Unsubscribe(endpointName, eventType);
        }

        ISubscriptionStore subscriptionStore;
        string endpointName;
        string localAddress;
    }
}