namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;


    sealed class CachedSubscriptionStore : ISubscriptionStore, IDisposable
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
                await Clear(CacheKey(eventType))
                    .ConfigureAwait(false);
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
                await Clear(CacheKey(eventType))
                    .ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (Cache.IsEmpty)
            {
                return;
            }

            foreach (var subscription in Cache.Values)
            {
                subscription.Dispose();
            }

            Cache.Clear();
        }

#pragma warning disable PS0018 // Clear should not be cancellable
        ValueTask Clear(string cacheKey) => Cache.TryGetValue(cacheKey, out var cachedSubscriptions) ? cachedSubscriptions.Clear() : default;
#pragma warning restore PS0018

        static string CacheKey(Type eventType) => eventType.FullName;

        readonly ConcurrentDictionary<string, CachedSubscriptions> Cache = new();
        readonly ISubscriptionStore inner;
        readonly TimeSpan cacheFor;

        public CachedSubscriptionStore(ISubscriptionStore inner, TimeSpan cacheFor)
        {
            this.inner = inner;
            this.cacheFor = cacheFor;
        }

        sealed class CachedSubscriptions : IDisposable
        {
            readonly SemaphoreSlim fetchSemaphore = new(1, 1);

            List<string> cachedSubscriptions;
            long cachedAtTimestamp;
            readonly ISubscriptionStore store;
            readonly Type eventType;
            readonly TimeSpan cacheFor1;

            public CachedSubscriptions(ISubscriptionStore store, Type eventType, TimeSpan cacheFor)
            {
                this.store = store;
                this.eventType = eventType;
                cacheFor1 = cacheFor;
            }

            public async ValueTask<List<string>> EnsureFresh(CancellationToken cancellationToken = default)
            {
                var cachedSubscriptionsSnapshot = cachedSubscriptions;
                var cachedAtTimestampSnapshot = cachedAtTimestamp;

                if (cachedSubscriptionsSnapshot != null && GetElapsedTime(cachedAtTimestampSnapshot) < cacheFor1)
                {
                    return cachedSubscriptionsSnapshot;
                }

                await fetchSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    if (cachedSubscriptions != null && GetElapsedTime(cachedAtTimestamp) < cacheFor1)
                    {
                        return cachedSubscriptions;
                    }

                    cachedSubscriptions = await store.GetSubscribers(eventType, cancellationToken).ConfigureAwait(false);
                    cachedAtTimestamp = Stopwatch.GetTimestamp();

                    return cachedSubscriptions;
                }
                finally
                {
                    fetchSemaphore.Release();
                }
            }

            static readonly double tickFrequency = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;
            static TimeSpan GetElapsedTime(long startingTimestamp) =>
                GetElapsedTime(startingTimestamp, Stopwatch.GetTimestamp());
            static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
                new((long)((endingTimestamp - startingTimestamp) * tickFrequency));

#pragma warning disable PS0018 // Clear should not be cancellable
            public async ValueTask Clear()
#pragma warning restore PS0018
            {
                try
                {
                    await fetchSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);

                    cachedSubscriptions = null;
                    cachedAtTimestamp = 0;
                }
                finally
                {
                    fetchSemaphore.Release();
                }
            }

            public void Dispose() => fetchSemaphore.Dispose();
        }
    }
}