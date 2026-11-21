namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class PolymorphicSubscriptionStore(SubscriptionTable subscriptionTable) : ISubscriptionStore
    {
        public Task<List<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default)
        {
            var topics = GetTopics(eventType);
            return subscriptionTable.GetSubscribers([.. topics], cancellationToken);
        }

        public Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken = default) => subscriptionTable.Subscribe(endpointName, endpointAddress, TopicName.From(eventType), cancellationToken);

        public Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default) => subscriptionTable.Unsubscribe(endpointName, TopicName.From(eventType), cancellationToken);

        IEnumerable<string> GetTopics(Type messageType) => eventTypeToTopicListMap.GetOrAdd(messageType, GenerateTopics);

        static string[] GenerateTopics(Type messageType) =>
        [
            .. GenerateMessageHierarchy(messageType)
                .Select(TopicName.From)
        ];

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
    }
}