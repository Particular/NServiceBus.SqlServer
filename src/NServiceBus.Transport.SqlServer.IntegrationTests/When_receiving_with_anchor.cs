namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Transport.Sql.Shared;
    using NUnit.Framework;
    using SqlServer;
    using Transport;

    public class When_receiving_with_anchor
    {
        SqlServerConstants sqlConstants = new();

        [Test]
        public async Task Anchored_receive_consumes_in_order_and_reports_row_versions()
        {
            using (var connection = await dbConnectionFactory.OpenNewConnection())
            {
                await Send(connection, "m1");
                await Send(connection, "m2");
                await Send(connection, "m3");

                var first = await queue.TryReceive(connection, null, 0);
                var second = await queue.TryReceive(connection, null, first.RowVersion);
                var third = await queue.TryReceive(connection, null, second.RowVersion);
                var pastEnd = await queue.TryReceive(connection, null, third.RowVersion);
                var headRescan = await queue.TryReceive(connection, null, 0);

                Assert.Multiple(() =>
                {
                    Assert.That(first.Message.Headers[Headers.MessageId], Is.EqualTo("m1"));
                    Assert.That(second.Message.Headers[Headers.MessageId], Is.EqualTo("m2"));
                    Assert.That(third.Message.Headers[Headers.MessageId], Is.EqualTo("m3"));
                    Assert.That(first.RowVersion, Is.GreaterThan(0));
                    Assert.That(second.RowVersion, Is.GreaterThan(first.RowVersion));
                    Assert.That(third.RowVersion, Is.GreaterThan(second.RowVersion));
                    Assert.That(pastEnd.Message, Is.Null, "nothing past the last consumed row");
                    Assert.That(headRescan.Message, Is.Null, "queue drained");
                });
            }
        }

        [Test]
        public async Task Head_rescan_finds_messages_behind_the_anchor()
        {
            using (var connection = await dbConnectionFactory.OpenNewConnection())
            {
                await Send(connection, "m1");
                await Send(connection, "m2");

                var probe = await queue.TryReceive(connection, null, 0);
                var pastFirst = probe.RowVersion;

                // an anchor past m1's row version skips m2's predecessor... receive whatever is past m1
                var second = await queue.TryReceive(connection, null, pastFirst);
                Assert.That(second.Message.Headers[Headers.MessageId], Is.EqualTo("m2"));

                // m1 was already consumed by the probe; send another and skip it with a far anchor
                await Send(connection, "m3");
                await Send(connection, "m4");

                var skipped = await queue.TryReceive(connection, null, second.RowVersion + 1);
                var foundOnRescan = await queue.TryReceive(connection, null, 0);

                Assert.Multiple(() =>
                {
                    Assert.That(skipped.Message.Headers[Headers.MessageId], Is.EqualTo("m4"), "anchor past m3 must skip it");
                    Assert.That(foundOnRescan.Message.Headers[Headers.MessageId], Is.EqualTo("m3"), "head rescan (anchor 0) must find the skipped message");
                });
            }
        }

        async Task Send(System.Data.Common.DbConnection connection, string messageId, CancellationToken cancellationToken = default)
        {
            var headers = new System.Collections.Generic.Dictionary<string, string> { [Headers.MessageId] = messageId };
            var message = new OutgoingMessage(messageId, headers, new byte[0]);
            await queue.Send(message, TimeSpan.MaxValue, connection, null, cancellationToken);
        }

        [SetUp]
        public void Prepare()
        {
            PrepareAsync().GetAwaiter().GetResult();
        }

        async Task PrepareAsync(CancellationToken cancellationToken = default)
        {
            var addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, new QueueSchemaAndCatalogOptions());

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString);

            var queueCreator = new QueueCreator(sqlConstants, dbConnectionFactory, addressTranslator.Parse);
            await queueCreator.CreateQueueIfNecessary(new[] { ValidAddress }, new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"), cancellationToken);

            var queueAddress = addressTranslator.Parse(ValidAddress);
            queue = new SqlTableBasedQueue(sqlConstants, queueAddress, queueAddress.Address, true);

            var purger = new QueuePurger(dbConnectionFactory);
            await purger.Purge(queue, cancellationToken);
        }

        TableBasedQueue queue;
        SqlServerDbConnectionFactory dbConnectionFactory;

        const string ValidAddress = "AnchorTests";
    }
}
