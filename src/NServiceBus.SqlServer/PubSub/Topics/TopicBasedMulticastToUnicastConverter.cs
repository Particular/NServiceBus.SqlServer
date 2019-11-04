namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    class TopicBasedMulticastToUnicastConverter : IMulticastToUnicastConverter
    {
        ITopicManager topicManager;
        ISubscriptionStore subscriptions;

        public TopicBasedMulticastToUnicastConverter(ITopicManager topicManager, ISubscriptionStore subscriptions)
        {
            this.topicManager = topicManager;
            this.subscriptions = subscriptions;
        }

        public async Task<List<UnicastTransportOperation>> Convert(MulticastTransportOperation transportOperation)
        {
            var topics = topicManager.GetTopicsFor(transportOperation.MessageType);

            var topicDestinations = await Task.WhenAll(topics.Select(subscriptions.GetSubscribersForTopic))
                .ConfigureAwait(false);
                
            return (from topicDestination in topicDestinations
                from destination in topicDestination
                select new UnicastTransportOperation(
                    transportOperation.Message,
                    destination,
                    transportOperation.RequiredDispatchConsistency,
                    transportOperation.DeliveryConstraints
                )).ToList();
        }
    }
}