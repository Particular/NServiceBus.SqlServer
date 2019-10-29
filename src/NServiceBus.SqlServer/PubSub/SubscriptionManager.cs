namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Settings;

    class SubscriptionManager : IManageSubscriptions
    {
        IManageTransportSubscriptions subscriptions;
        string endpointName;
        string localAddress;

        public SubscriptionManager(IManageTransportSubscriptions subscriptions, ReadOnlySettings settings)
        {
            this.subscriptions = subscriptions;
            endpointName = settings.EndpointName();
            localAddress = settings.LocalAddress();
        }

        public Task Subscribe(Type eventType, ContextBag context)
        {
            return subscriptions.Subscribe(
                endpointName,
                localAddress,
                eventType.ToString()
            );
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            return subscriptions.Unsubscribe(
                endpointName,
                eventType.ToString()
            );
        }
    }
}