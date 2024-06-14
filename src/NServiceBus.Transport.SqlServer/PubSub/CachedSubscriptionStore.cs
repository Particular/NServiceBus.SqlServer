namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;


    class CachedSubscriptionStore : ISubscriptionStore
    {
        public CachedSubscriptionStore(ISubscriptionStore inner, TimeSpan cacheFor)
        {
            this.inner = inner;
            this.cacheFor = cacheFor;
        }

        public Task<List<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default)
        {
            var cacheItem = Cache.GetOrAdd(CacheKey(eventType),
                _ => new CacheItem
                {
                    StoredUtc = DateTime.UtcNow,
                    Subscribers = inner.GetSubscribers(eventType, cancellationToken)
                });

            var age = DateTime.UtcNow - cacheItem.StoredUtc;
            if (age >= cacheFor)
            {
                cacheItem.Subscribers = inner.GetSubscribers(eventType, cancellationToken);
                cacheItem.StoredUtc = DateTime.UtcNow;
            }

            return cacheItem.Subscribers;
        }

        public async Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken = default)
        {
            await inner.Subscribe(endpointName, endpointAddress, eventType, cancellationToken).ConfigureAwait(false);
            ClearForMessageType(CacheKey(eventType));
        }

        public async Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default)
        {
            await inner.Unsubscribe(endpointName, eventType, cancellationToken).ConfigureAwait(false);
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
            public DateTime StoredUtc { get; set; } // Internal usage, only set/get using private
            public Task<List<string>> Subscribers { get; set; }
        }
    }
}