﻿namespace NServiceBus.Transport.SqlServer.UnitTests
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

            Assert.Multiple(() =>
            {
                Assert.That(transport.Transport.ConnectionString, Is.EqualTo("connectionString"));
                Assert.That(transport.Transport.DefaultSchema, Is.EqualTo("schema"));
                Assert.That(transport.Transport.ExpiredMessagesPurger.PurgeOnStartup, Is.EqualTo(true));
                Assert.That(transport.Transport.ExpiredMessagesPurger.PurgeBatchSize, Is.EqualTo(100));
                Assert.That(transport.Transport.QueuePeeker.Delay, Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(transport.Transport.QueuePeeker.MaxRecordsToPeek, Is.EqualTo(100));

                Assert.That(transport.Transport.DelayedDelivery.TableSuffix, Is.EqualTo("suffix"));
                Assert.That(transport.Transport.DelayedDelivery.BatchSize, Is.EqualTo(100));

                Assert.That(transport.Transport.Subscriptions.DisableCaching, Is.EqualTo(true));
                Assert.That(transport.Transport.Subscriptions.CacheInvalidationPeriod, Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(transport.Transport.Subscriptions.SubscriptionTableName.Qualify("dbo", "nsb").QuotedQualifiedName, Is.EqualTo("[catalog].[schema].[table]"));
            });
        }
    }
}