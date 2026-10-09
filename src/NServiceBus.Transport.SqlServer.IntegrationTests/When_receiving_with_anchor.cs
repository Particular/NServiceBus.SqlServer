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
                    Assert.That(pastEnd.Successful, Is.False, "nothing past the last consumed row");
                    Assert.That(headRescan.Successful, Is.False, "queue drained");
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

        [Test]
        public async Task Head_rescan_finds_rows_committed_behind_the_anchor()
        {
            using (var slowSender = await dbConnectionFactory.OpenNewConnection())
            using (var receiver = await dbConnectionFactory.OpenNewConnection())
            {
                long earlyRowVersion;

                // the slow sender is allocated the lower row version but commits last
                using (var slowTransaction = slowSender.BeginTransaction())
                {
                    await Send(slowSender, "committed-late", slowTransaction);
                    await Send(receiver, "committed-early");

                    var early = await queue.TryReceive(receiver, null, 0);
                    Assert.That(early.Message.Headers[Headers.MessageId], Is.EqualTo("committed-early"));
                    earlyRowVersion = early.RowVersion;

                    await slowTransaction.CommitAsync();
                }

                var anchoredReceive = await queue.TryReceive(receiver, null, earlyRowVersion);
                var late = await queue.TryReceive(receiver, null, 0);

                Assert.Multiple(() =>
                {
                    Assert.That(anchoredReceive.Successful, Is.False, "the late commit is behind the anchor");
                    Assert.That(late.Message.Headers[Headers.MessageId], Is.EqualTo("committed-late"));
                    Assert.That(late.RowVersion, Is.LessThan(earlyRowVersion));
                });
            }
        }

        [Test]
        public async Task Peek_reports_the_lowest_row_committed_behind_the_anchor()
        {
            using (var slowSender = await dbConnectionFactory.OpenNewConnection())
            using (var receiver = await dbConnectionFactory.OpenNewConnection())
            {
                long earlyRowVersion;

                using (var slowTransaction = slowSender.BeginTransaction())
                {
                    await Send(slowSender, "committed-late", slowTransaction);
                    await Send(receiver, "committed-early");

                    earlyRowVersion = (await queue.TryReceive(receiver, null, 0)).RowVersion;

                    await slowTransaction.CommitAsync();
                }

                queue.FormatPeekCommand();
                var peek = await queue.TryPeek(receiver, null);
                var fromPeekAnchor = await queue.TryReceive(receiver, null, peek.LowestRowVersion - 1);

                Assert.Multiple(() =>
                {
                    Assert.That(peek.MessageCount, Is.EqualTo(1));
                    Assert.That(peek.LowestRowVersion, Is.LessThan(earlyRowVersion), "the late commit is behind the consumed row");
                    Assert.That(fromPeekAnchor.Message.Headers[Headers.MessageId], Is.EqualTo("committed-late"));
                });
            }
        }

        [Test]
        public async Task Peek_skips_rows_locked_by_an_in_flight_receive()
        {
            using (var inFlight = await dbConnectionFactory.OpenNewConnection())
            using (var peeker = await dbConnectionFactory.OpenNewConnection())
            {
                await Send(peeker, "in-flight");
                await Send(peeker, "available");

                using (var receiveTransaction = inFlight.BeginTransaction())
                {
                    var locked = await queue.TryReceive(inFlight, receiveTransaction, 0);

                    queue.FormatPeekCommand();
                    var peek = await queue.TryPeek(peeker, null);
                    var fromPeekAnchor = await queue.TryReceive(peeker, null, peek.LowestRowVersion - 1);

                    Assert.Multiple(() =>
                    {
                        Assert.That(peek.LowestRowVersion, Is.GreaterThan(locked.RowVersion));
                        Assert.That(fromPeekAnchor.Message.Headers[Headers.MessageId], Is.EqualTo("available"));
                    });

                    await receiveTransaction.RollbackAsync();
                }
            }
        }

        [Test]
        public async Task Peek_of_an_empty_queue_reports_no_row()
        {
            using (var connection = await dbConnectionFactory.OpenNewConnection())
            {
                queue.FormatPeekCommand();
                var peek = await queue.TryPeek(connection, null);

                Assert.Multiple(() =>
                {
                    Assert.That(peek.MessageCount, Is.EqualTo(0));
                    Assert.That(peek.LowestRowVersion, Is.EqualTo(0));
                });
            }
        }

        async Task Send(System.Data.Common.DbConnection connection, string messageId, System.Data.Common.DbTransaction transaction = null, CancellationToken cancellationToken = default)
        {
            var headers = new System.Collections.Generic.Dictionary<string, string> { [Headers.MessageId] = messageId };
            var message = new OutgoingMessage(messageId, headers, new byte[0]);
            await queue.Send(message, TimeSpan.MaxValue, connection, transaction, cancellationToken);
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
