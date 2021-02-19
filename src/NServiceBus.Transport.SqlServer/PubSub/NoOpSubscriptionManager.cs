namespace NServiceBus.Transport.SqlServer.PubSub
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Unicast.Messages;

    class NoOpSubscriptionManager : ISubscriptionManager
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public Task Subscribe(MessageMetadata eventType, ContextBag context)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            return Task.CompletedTask;
        }

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