namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class SubscriptionCache : ISubscriptionStore
    {
        public SubscriptionCache(ISubscriptionStore inner, TimeSpan cacheFor)
        {
            this.inner = inner;
            this.cacheFor = cacheFor;
        }

        public Task<List<string>> GetSubscribersForTopic(string topic)
        {
            var cacheItem = Cache.GetOrAdd(topic,
                _ => new CacheItem
                {
                    Stored = DateTime.UtcNow,
                    Subscribers = inner.GetSubscribersForTopic(topic)
                });

            var age = DateTime.UtcNow - cacheItem.Stored;
            if (age >= cacheFor)
            {
                cacheItem.Subscribers = inner.GetSubscribersForTopic(topic);
                cacheItem.Stored = DateTime.UtcNow;
            }

            return cacheItem.Subscribers;
        }

        public async Task Subscribe(string endpointName, string endpointAddress, string topic)
        {
            await inner.Subscribe(endpointName, endpointAddress, topic).ConfigureAwait(false);
            ClearForMessageType(topic);
        }

        public async Task Unsubscribe(string endpointName, string topic)
        {
            await inner.Unsubscribe(endpointName, topic).ConfigureAwait(false);
            ClearForMessageType(topic);
        }

        void ClearForMessageType(string topice)
        {
            Cache.TryRemove(topice, out _);
        }

        TimeSpan cacheFor;
        ISubscriptionStore inner;
        ConcurrentDictionary<string, CacheItem> Cache = new ConcurrentDictionary<string, CacheItem>();

        class CacheItem
        {
            public DateTime Stored { get; set; }
            public Task<List<string>> Subscribers { get; set; }
        }
    }
}