namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;


    class CachedSubscriptionStore(ISubscriptionStore inner, TimeSpan cacheFor) : ISubscriptionStore
    {
        public async Task<List<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default)
        {
            var cacheKey = CacheKey(eventType);
            var cachedSubscriptions = Cache.GetOrAdd(cacheKey,
                static (_, state) => new CachedSubscriptions(state.inner, state.eventType, state.cacheFor),
                (inner, eventType, cacheFor));

            return await cachedSubscriptions.EnsureFresh(cancellationToken).ConfigureAwait(false);
        }

        public async Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken = default)
        {
            try
            {
                await inner.Subscribe(endpointName, endpointAddress, eventType, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ClearForMessageType(CacheKey(eventType));
            }
        }

        public async Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default)
        {
            try
            {
                await inner.Unsubscribe(endpointName, eventType, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ClearForMessageType(CacheKey(eventType));
            }
        }

        void ClearForMessageType(string topic)
        {
            if (Cache.TryRemove(topic, out var removed))
            {
                removed.Dispose();
            }
        }

        static string CacheKey(Type eventType) => eventType.FullName;

        readonly ConcurrentDictionary<string, CachedSubscriptions> Cache = new();

        sealed class CachedSubscriptions(ISubscriptionStore store, Type eventType, TimeSpan cacheFor)
            : IDisposable
        {
            SemaphoreSlim fetchSemaphore = new(1, 1);

            List<string> cachedData;
            DateTime cachedAt;

            public async ValueTask<List<string>> EnsureFresh(CancellationToken cancellationToken = default)
            {
                if (cachedData != null && DateTime.UtcNow - cachedAt < cacheFor)
                {
                    return cachedData;
                }

                using var lease = await AcquireLease(cancellationToken).ConfigureAwait(false);

                if (cachedData != null && DateTime.UtcNow - cachedAt < cacheFor)
                {
                    return cachedData;
                }

                cachedData = await store.GetSubscribers(eventType, cancellationToken).ConfigureAwait(false);
                cachedAt = DateTime.UtcNow;

                return cachedData;
            }

            async ValueTask<FetchLease> AcquireLease(CancellationToken cancellationToken)
            {
                await fetchSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new FetchLease(fetchSemaphore);
            }

            public void Dispose()
            {
                if (fetchSemaphore is null)
                {
                    return;
                }

                fetchSemaphore.Dispose();
                fetchSemaphore = null;
            }

            readonly struct FetchLease : IDisposable
            {
                readonly SemaphoreSlim semaphore;

                internal FetchLease(SemaphoreSlim semaphore) => this.semaphore = semaphore;

                public void Dispose() => semaphore.Release();
            }
        }
    }
}