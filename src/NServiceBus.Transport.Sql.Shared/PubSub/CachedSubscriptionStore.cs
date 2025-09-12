namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;


    sealed class CachedSubscriptionStore(ISubscriptionStore inner, TimeSpan cacheFor) : ISubscriptionStore, IDisposable
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
        ValueTask Clear(string cacheKey) => Cache.TryGetValue(cacheKey, out var cachedSubscriptions) ? cachedSubscriptions.Clear() : ValueTask.CompletedTask;
#pragma warning restore PS0018

        static string CacheKey(Type eventType) => eventType.FullName;

        readonly ConcurrentDictionary<string, CachedSubscriptions> Cache = new();

        sealed class CachedSubscriptions(ISubscriptionStore store, Type eventType, TimeSpan cacheFor) : IDisposable
        {
            readonly SemaphoreSlim fetchSemaphore = new(1, 1);

            List<string> cachedSubscriptions;
            long cachedAtTimestamp;

            public async ValueTask<List<string>> EnsureFresh(CancellationToken cancellationToken = default)
            {
                var cachedSubscriptionsSnapshot = cachedSubscriptions;
                var cachedAtTimestampSnapshot = cachedAtTimestamp;

                if (cachedSubscriptionsSnapshot != null && Stopwatch.GetElapsedTime(cachedAtTimestampSnapshot) < cacheFor)
                {
                    return cachedSubscriptionsSnapshot;
                }

                using var lease = await AcquireLease(cancellationToken).ConfigureAwait(false);

                if (cachedSubscriptions != null && Stopwatch.GetElapsedTime(cachedAtTimestamp) < cacheFor)
                {
                    return cachedSubscriptions;
                }

                cachedSubscriptions = await store.GetSubscribers(eventType, cancellationToken).ConfigureAwait(false);
                cachedAtTimestamp = Stopwatch.GetTimestamp();

                return cachedSubscriptions;
            }

#pragma warning disable PS0018 // Clear should not be cancellable
            public async ValueTask Clear()
#pragma warning restore PS0018
            {
                using var lease = await AcquireLease(CancellationToken.None).ConfigureAwait(false);
                cachedSubscriptions = null;
                cachedAtTimestamp = 0;
            }

            public void Dispose() => fetchSemaphore.Dispose();

            async ValueTask<FetchLease> AcquireLease(CancellationToken cancellationToken)
            {
                await fetchSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new FetchLease(fetchSemaphore);
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