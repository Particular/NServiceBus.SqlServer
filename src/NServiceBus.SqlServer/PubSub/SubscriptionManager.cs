namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Settings;

    class SubscriptionManager : IManageSubscriptions
    {
        TableBasedSubscriptions tableBasedSubscriptions;
        string endpointName;
        string localAddress;

        public SubscriptionManager(TableBasedSubscriptions tableBasedSubscriptions, ReadOnlySettings settings)
        {
            this.tableBasedSubscriptions = tableBasedSubscriptions;
            endpointName = settings.EndpointName();
            localAddress = settings.LocalAddress();
        }

        public Task Subscribe(Type eventType, ContextBag context)
        {
            return tableBasedSubscriptions.Subscribe(
                endpointName,
                localAddress,
                eventType.ToString()
            );
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            return tableBasedSubscriptions.Unsubscribe(
                endpointName,
                eventType.ToString()
            );
        }
    }
}