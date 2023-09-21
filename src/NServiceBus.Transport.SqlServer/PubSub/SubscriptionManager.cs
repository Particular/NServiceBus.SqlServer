namespace NServiceBus.Transport.SqlServer
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using NServiceBus.Unicast.Messages;

    class SubscriptionManager(ISubscriptionStore subscriptionStore, string endpointName, string localAddress) : ISubscriptionManager
    {
        public Task Subscribe(MessageMetadata eventType, CancellationToken cancellationToken = default) => subscriptionStore.Subscribe(endpointName, localAddress, eventType.MessageType, cancellationToken);

        public Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default) => Task.WhenAll(eventTypes.Select(et => Subscribe(et, cancellationToken)));

        public Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default) => subscriptionStore.Unsubscribe(endpointName, eventType.MessageType, cancellationToken);
    }
}