namespace NServiceBus.Transport.SqlServer.UnitTests
{
    using System;
    using NUnit.Framework;

    public class LegacyApiTests
    {
        [Test]
        public void SupportsLegacyUseTransportApi()
        {
            var configuration = new EndpointConfiguration("name");

            var transport = configuration.UseTransport<SqlServerTransport>();
            transport.ConnectionString("connectionString");
            transport.DefaultSchema("schema");
            transport.PurgeExpiredMessagesOnStartup(100);
            transport.QueuePeekerOptions(TimeSpan.FromSeconds(1), 100);

            var nativeDelayedDelivery = transport.NativeDelayedDelivery();
            nativeDelayedDelivery.TableSuffix("suffix");
            nativeDelayedDelivery.BatchSize(100);

            var subscriptionSettings = transport.SubscriptionSettings();
            subscriptionSettings.SubscriptionTableName("table", "schema", "catalog");
            subscriptionSettings.CacheSubscriptionInformationFor(TimeSpan.FromSeconds(1));
            subscriptionSettings.DisableSubscriptionCache();

            Assert.AreEqual("connectionString", transport.Transport.ConnectionString);
            Assert.AreEqual("schema", transport.Transport.DefaultSchema);
            Assert.AreEqual(true, transport.Transport.ExpiredMessagesPurger.PurgeOnStartup);
            Assert.AreEqual(100, transport.Transport.ExpiredMessagesPurger.PurgeBatchSize);
            Assert.AreEqual(TimeSpan.FromSeconds(1), transport.Transport.QueuePeeker.Delay);
            Assert.AreEqual(100, transport.Transport.QueuePeeker.MaxRecordsToPeek);

            Assert.AreEqual("suffix", transport.Transport.DelayedDelivery.TableSuffix);
            Assert.AreEqual(100, transport.Transport.DelayedDelivery.BatchSize);

            Assert.AreEqual(true, transport.Transport.Subscriptions.DisableCaching);
            Assert.AreEqual(TimeSpan.FromSeconds(1), transport.Transport.Subscriptions.CacheInvalidationPeriod);
            Assert.AreEqual("[catalog].[schema].[table]", transport.Transport.Subscriptions.SubscriptionTableName.Qualify("dbo", "nsb", new SqlServerNameHelper()).QuotedQualifiedName);
        }
    }
}