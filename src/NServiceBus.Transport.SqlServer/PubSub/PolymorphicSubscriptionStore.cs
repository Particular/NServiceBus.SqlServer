namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class PolymorphicSubscriptionStore : ISubscriptionStore
    {
        public PolymorphicSubscriptionStore(SubscriptionTable subscriptionTable)
        {
            this.subscriptionTable = subscriptionTable;
        }

        public Task<List<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default)
        {
            var topics = GetTopics(eventType);
            return subscriptionTable.GetSubscribers(topics.ToArray(), cancellationToken);
        }

        public Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken = default)
        {
            return subscriptionTable.Subscribe(endpointName, endpointAddress, TopicName.From(eventType), cancellationToken);
        }

        public Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default)
        {
            return subscriptionTable.Unsubscribe(endpointName, TopicName.From(eventType), cancellationToken);
        }

        IEnumerable<string> GetTopics(Type messageType)
        {
            return eventTypeToTopicListMap.GetOrAdd(messageType, GenerateTopics);
        }

        static string[] GenerateTopics(Type messageType)
        {
            return GenerateMessageHierarchy(messageType)
                .Select(TopicName.From)
                .ToArray();
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

        ConcurrentDictionary<Type, string[]> eventTypeToTopicListMap = new ConcurrentDictionary<Type, string[]>();
        SubscriptionTable subscriptionTable;
    }
}