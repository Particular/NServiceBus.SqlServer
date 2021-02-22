namespace NServiceBus.Transport.SqlServer
{
    using System.Linq;
    using Unicast.Messages;
    using System.Threading.Tasks;
    using Extensibility;

    class SubscriptionManager : ISubscriptionManager
    {
        public SubscriptionManager(ISubscriptionStore subscriptionStore, string endpointName, string localAddress)
        {
            this.subscriptionStore = subscriptionStore;
            this.endpointName = endpointName;
            this.localAddress = localAddress;
        }

#pragma warning disable IDE0060 // Remove unused parameter
        public Task Subscribe(MessageMetadata eventType, ContextBag context)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            return subscriptionStore.Subscribe(endpointName, localAddress, eventType.MessageType);
        }

        public Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context)
        {
            return Task.WhenAll(eventTypes.Select(et => Subscribe(et, context)));
        }

        public Task Unsubscribe(MessageMetadata eventType, ContextBag context)
        {
            return subscriptionStore.Unsubscribe(endpointName, eventType.MessageType);
        }

        ISubscriptionStore subscriptionStore;
        string endpointName;
        string localAddress;
    }
}