namespace NServiceBus.Transport.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class CachedSubscriptionStoreTests
    {
        // Reproduces a bug that previously existed in the original implementation that stored tasks and their outcomes in the cache (see #1588)
        // leaving this test around to make sure such a problem doesn't occur again.
        [Test]
        public void Should_not_cache_cancelled_operations()
        {
            var subscriptionStore = new FakeSubscriptionStore
            {
                GetSubscribersAction = (_, token) => token.IsCancellationRequested ? Task.FromCanceled<List<string>>(token) : Task.FromResult(new List<string>())
            };

            var cache = new CachedSubscriptionStore(subscriptionStore, TimeSpan.FromSeconds(60));

            Assert.Multiple(() =>
            {
                _ = Assert.ThrowsAsync<TaskCanceledException>(async () =>
                    await cache.GetSubscribers(typeof(object), new CancellationToken(true)));
                Assert.DoesNotThrowAsync(async () => await cache.GetSubscribers(typeof(object), CancellationToken.None));
            });
        }

        class FakeSubscriptionStore : ISubscriptionStore
        {
            public Func<Type, CancellationToken, Task<List<string>>> GetSubscribersAction { get; set; } =
                (type, token) => Task.FromResult(new List<string>());

            public Task<List<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default) =>
                GetSubscribersAction(eventType, cancellationToken);

            public Task Subscribe(string endpointName, string endpointAddress, Type eventType,
                CancellationToken cancellationToken = default) =>
                throw new NotImplementedException();

            public Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException();
        }
    }
}