namespace NServiceBus.Transport.SqlServer.PubSub
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Unicast.Messages;

    class NoOpSubscriptionManager : ISubscriptionManager
    {
        public Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}