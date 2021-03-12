#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable 618
#pragma warning disable 619

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
            nativeDelayedDelivery.ProcessingInterval(TimeSpan.FromSeconds(1));

            var subscriptionSettings = transport.SubscriptionSettings();
            subscriptionSettings.SubscriptionTableName("table", "schema", "catalog");
            subscriptionSettings.CacheSubscriptionInformationFor(TimeSpan.FromSeconds(1));
            subscriptionSettings.DisableSubscriptionCache();

            Assert.AreEqual("connectionString", transport.SqlTransport.ConnectionString);
            Assert.AreEqual("schema", transport.SqlTransport.DefaultSchema);
            Assert.AreEqual(true, transport.SqlTransport.ExpiredMessagesPurger.PurgeOnStartup);
            Assert.AreEqual(100, transport.SqlTransport.ExpiredMessagesPurger.PurgeBatchSize);
            Assert.AreEqual(TimeSpan.FromSeconds(1), transport.SqlTransport.QueuePeeker.Delay);
            Assert.AreEqual(100, transport.SqlTransport.QueuePeeker.MaxRecordsToPeek);

            Assert.AreEqual("suffix", transport.SqlTransport.DelayedDelivery.TableSuffix);
            Assert.AreEqual(100, transport.SqlTransport.DelayedDelivery.BatchSize);
            Assert.AreEqual(TimeSpan.FromSeconds(1), transport.SqlTransport.DelayedDelivery.ProcessingInterval);

            Assert.AreEqual(true, transport.SqlTransport.Subscriptions.DisableCaching);
            Assert.AreEqual(TimeSpan.FromSeconds(1), transport.SqlTransport.Subscriptions.CacheInvalidationPeriod);
            Assert.AreEqual("[catalog].[schema].[table]", transport.SqlTransport.Subscriptions.SubscriptionTableName.Qualify("dbo", "nsb").QuotedQualifiedName);
        }
    }
}
#pragma warning restore 618
#pragma warning restore 619
#pragma warning restore IDE0079 // Remove unnecessary suppression