using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Unicast.Messages;

namespace NServiceBus.Transport.SqlServer.PubSub
{
    class NoOpSubscriptionManager : ISubscriptionManager
    {
        public Task Subscribe(MessageMetadata eventType, ContextBag context)
        {
            return Task.CompletedTask;
        }

        public Task Unsubscribe(MessageMetadata eventType, ContextBag context)
        {
            return Task.CompletedTask;
        }
    }
}