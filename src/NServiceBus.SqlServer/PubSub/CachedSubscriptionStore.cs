namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;


    class CachedSubscriptionStore : ISubscriptionStore
    {
        public CachedSubscriptionStore(ISubscriptionStore inner, TimeSpan cacheFor)
        {
            this.inner = inner;
            this.cacheFor = cacheFor;
        }

        public Task<List<string>> GetSubscribers(Type eventType)
        {
            var cacheItem = Cache.GetOrAdd(CacheKey(eventType),
                _ => new CacheItem
                {
                    Stored = DateTime.UtcNow,
                    Subscribers = inner.GetSubscribers(eventType)
                });

            var age = DateTime.UtcNow - cacheItem.Stored;
            if (age >= cacheFor)
            {
                cacheItem.Subscribers = inner.GetSubscribers(eventType);
                cacheItem.Stored = DateTime.UtcNow;
            }

            return cacheItem.Subscribers;
        }

        public async Task Subscribe(string endpointName, string endpointAddress, Type eventType)
        {
            await inner.Subscribe(endpointName, endpointAddress, eventType).ConfigureAwait(false);
            ClearForMessageType(CacheKey(eventType));
        }

        public async Task Unsubscribe(string endpointName, Type eventType)
        {
            await inner.Unsubscribe(endpointName, eventType).ConfigureAwait(false);
            ClearForMessageType(CacheKey(eventType));
        }

        void ClearForMessageType(string topic)
        {
            Cache.TryRemove(topic, out _);
        }

        static string CacheKey(Type eventType)
        {
            return eventType.FullName;
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