namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    class MulticastToUnicastConverter : IMulticastToUnicastConverter
    {
        ISubscriptionStore subscriptions;
        ConcurrentDictionary<Type, Type[]> messageHierarchyCache = new ConcurrentDictionary<Type, Type[]>();

        public MulticastToUnicastConverter(ISubscriptionStore subscriptions)
        {
            this.subscriptions = subscriptions;
        }

        public async Task<List<UnicastTransportOperation>> Convert(MulticastTransportOperation transportOperation)
        {
            var topics = GetTopicsFor(transportOperation.MessageType);

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


        IEnumerable<string> GetTopicsFor(Type messageType)
        {
            return GetMessageHierarchy(messageType).Select(TopicName.From);
        }

        IEnumerable<Type> GetMessageHierarchy(Type messageType)
        {
            return messageHierarchyCache.GetOrAdd(messageType, t => GenerateMessageHierarchy(t).ToArray());
        }

        static IEnumerable<Type> GenerateMessageHierarchy(Type messageType)
        {
            var t = messageType;
            while (t != null)
            {
                yield return t;
                t = t.BaseType;
            }
            foreach (var iface in messageType.GetInterfaces())
            {
                yield return iface;
            }
        }
    }
}