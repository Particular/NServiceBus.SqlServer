using System;
using System.Threading.Tasks;

namespace NServiceBus.SqlServer.IntegrationTests
{
    using System.Data.SqlClient;
    using System.Linq;
    using NServiceBus.Extensibility;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    [TestFixture]
    public class SubscriptionManagerTests
    {
        [Test]
        public async Task It_inserts_subscription_entries()
        {
            var endpoint = Guid.NewGuid().ToString();
            var transportAddress = Guid.NewGuid().ToString();
            var manager = CreateSubscriptionManager(endpoint, transportAddress);
            var reader = CreateSubscriptionReadeer();
            await manager.Subscribe(typeof(MyEvent), new ContextBag());

            var insertedValues = await reader.GetSubscribersFor(typeof(MyEvent));
            var list = insertedValues.ToList();

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(endpoint, list[0].Endpoint);
            Assert.AreEqual(transportAddress, list[0].TransportAddress);
        }

        static SubscriptionManager CreateSubscriptionManager(string endpoint, string transportAddress)
        {
            var manager = new SubscriptionManager(endpoint, transportAddress, "dbo", "Subscriptions", new SqlConnectionFactory(() =>
            {
                var connection = new SqlConnection(@"Server=.\sqlexpress;Database=nservicebus;Trusted_Connection=True");
                connection.Open();
                return Task.FromResult(connection);
            }));
            return manager;
        }

        static SubscriptionReader CreateSubscriptionReadeer()
        {
            var reader = new SubscriptionReader("dbo", "Subscriptions", new SqlConnectionFactory(() =>
            {
                var connection = new SqlConnection(@"Server=.\sqlexpress;Database=nservicebus;Trusted_Connection=True");
                connection.Open();
                return Task.FromResult(connection);
            }), new [] {typeof(MyEvent), typeof(MyOtherEvent), typeof(MyPolimorphic)});
            return reader;
        }

        [Test]
        public async Task Subscription_insert_is_idempotent()
        {
            var endpoint = Guid.NewGuid().ToString();
            var transportAddress = Guid.NewGuid().ToString();
            var manager = CreateSubscriptionManager(endpoint, transportAddress);
            var reader = CreateSubscriptionReadeer();

            await manager.Subscribe(typeof(MyEvent), new ContextBag());
            await manager.Subscribe(typeof(MyEvent), new ContextBag());

            var insertedValues = await reader.GetSubscribersFor(typeof(MyEvent));
            var list = insertedValues.ToList();

            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public async Task It_returns_multiple_event_subscriptions()
        {
            var endpoint = Guid.NewGuid().ToString();
            var transportAddress = Guid.NewGuid().ToString();
            var manager = CreateSubscriptionManager(endpoint, transportAddress);
            var reader = CreateSubscriptionReadeer();

            await manager.Subscribe(typeof(MyOtherEvent), new ContextBag());

            var insertedValues = await reader.GetSubscribersFor(typeof(MyPolimorphic));
            var list = insertedValues.ToList();

            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public async Task It_returns_entries_only_if_subscription_exists()
        {
            var endpoint = Guid.NewGuid().ToString();
            var transportAddress = Guid.NewGuid().ToString();
            var manager = CreateSubscriptionManager(endpoint, transportAddress);
            var reader = CreateSubscriptionReadeer();

            await manager.Subscribe(typeof(MyEvent), new ContextBag());

            var insertedValues = await reader.GetSubscribersFor(typeof(MyOtherEvent));
            var list = insertedValues.ToList();

            Assert.AreEqual(0, list.Count);
        }

        public class MyEvent
        {
        }

        public class MyOtherEvent
        {
             
        }

        public class MyPolimorphic : MyOtherEvent
        {

        }
    }
}
