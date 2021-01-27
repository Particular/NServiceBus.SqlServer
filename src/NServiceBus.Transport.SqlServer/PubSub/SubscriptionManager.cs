using System.Linq;
using NServiceBus.Unicast.Messages;

namespace NServiceBus.Transport.SqlServer
{
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

        public Task Subscribe(MessageMetadata eventType, ContextBag context)
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