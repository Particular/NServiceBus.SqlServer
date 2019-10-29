namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class SubscriptionCache : IManageTransportSubscriptions
    {
        public SubscriptionCache(IManageTransportSubscriptions inner, TimeSpan cacheFor)
        {
            this.inner = inner;
            this.cacheFor = cacheFor;
        }

        public Task<List<string>> GetSubscribersForEvent(string eventType)
        {
            var cacheItem = Cache.GetOrAdd(eventType,
                _ => new CacheItem
                {
                    Stored = DateTime.UtcNow,
                    Subscribers = inner.GetSubscribersForEvent(eventType)
                });

            var age = DateTime.UtcNow - cacheItem.Stored;
            if (age >= cacheFor)
            {
                cacheItem.Subscribers = inner.GetSubscribersForEvent(eventType);
                cacheItem.Stored = DateTime.UtcNow;
            }

            return cacheItem.Subscribers;
        }

        public async Task Subscribe(string endpointName, string endpointAddress, string eventType)
        {
            await inner.Subscribe(endpointName, endpointAddress, eventType).ConfigureAwait(false);
            ClearForMessageType(eventType);
        }

        public async Task Unsubscribe(string endpointName, string eventType)
        {
            await inner.Unsubscribe(endpointName, eventType).ConfigureAwait(false);
            ClearForMessageType(eventType);
        }

        void ClearForMessageType(string eventType)
        {
            Cache.TryRemove(eventType, out _);
        }

        TimeSpan cacheFor;
        IManageTransportSubscriptions inner;
        ConcurrentDictionary<string, CacheItem> Cache = new ConcurrentDictionary<string, CacheItem>();

        class CacheItem
        {
            public DateTime Stored { get; set; }
            public Task<List<string>> Subscribers { get; set; }
        }
    }
}