namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using NServiceBus.Transport.Sql.Shared;
    using NUnit.Framework;
    using Performance.TimeToBeReceived;
    using Routing;
    using SqlServer;
    using Transport;

    using SettingsKeys = SettingsKeys;

    public class When_using_ttbr
    {
        SqlServerConstants sqlConstants = new();

        [Test]
        public async Task Defaults_to_no_ttbr()
        {
            using (var connection = dbConnectionFactory.OpenNewConnection().GetAwaiter().GetResult())
            {
                using (var transaction = connection.BeginTransaction())
                {
                    var transportTransaction = new TransportTransaction();

                    transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, connection);
                    transportTransaction.Set(SettingsKeys.TransportTransactionSqlTransactionKey, transaction);

                    var context = new ContextBag();
                    context.Set(transportTransaction);

                    var operation = new TransportOperation(
                        new OutgoingMessage("1", [], new byte[0]),
                        new UnicastAddressTag(ValidAddress)
                    );

                    await dispatcher.Dispatch(new TransportOperations(operation), transportTransaction).ConfigureAwait(false);
                    transaction.Commit();
                }

                var message = await queue.TryReceive(connection, null).ConfigureAwait(false);
                Assert.IsFalse(message.Message.Expired);
            }
        }

        [Test]
        public async Task Diagnostic_headers_are_ignored()
        {
            using (var connection = dbConnectionFactory.OpenNewConnection().GetAwaiter().GetResult())
            {
                using (var transaction = connection.BeginTransaction())
                {
                    var transportTransaction = new TransportTransaction();

                    transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, connection);
                    transportTransaction.Set(SettingsKeys.TransportTransactionSqlTransactionKey, transaction);

                    var context = new ContextBag();
                    context.Set(transportTransaction);

                    var headers = new Dictionary<string, string>
                    {
                        [Headers.TimeToBeReceived] = TimeSpan.FromMinutes(-1).ToString()
                    };
                    var operation = new TransportOperation(
                        new OutgoingMessage("1", headers, new byte[0]),
                        new UnicastAddressTag(ValidAddress)
                    );

                    await dispatcher.Dispatch(new TransportOperations(operation), transportTransaction).ConfigureAwait(false);
                    transaction.Commit();
                }

                var message = await queue.TryReceive(connection, null).ConfigureAwait(false);
                Assert.IsFalse(message.Message.Expired);
            }
        }

        [Test]
        public async Task Delivery_constraint_is_respected()
        {
            using (var connection = dbConnectionFactory.OpenNewConnection().GetAwaiter().GetResult())
            {
                using (var transaction = connection.BeginTransaction())
                {
                    var transportTransaction = new TransportTransaction();

                    transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, connection);
                    transportTransaction.Set(SettingsKeys.TransportTransactionSqlTransactionKey, transaction);

                    var context = new ContextBag();
                    context.Set(transportTransaction);

                    var operation = new TransportOperation(
                        new OutgoingMessage("1", [], new byte[0]),
                        new UnicastAddressTag(ValidAddress),
                        new DispatchProperties
                        {
                            DiscardIfNotReceivedBefore = new DiscardIfNotReceivedBefore(TimeSpan.FromMinutes(-1)) //Discard immediately
                        }
                    );

                    await dispatcher.Dispatch(new TransportOperations(operation), transportTransaction).ConfigureAwait(false);
                    transaction.Commit();
                }

                var message = await queue.TryReceive(connection, null).ConfigureAwait(false);
                Assert.IsTrue(message.Message.Expired);
            }
        }

        [SetUp]
        public void Prepare()
        {
            PrepareAsync().GetAwaiter().GetResult();
        }

        async Task PrepareAsync(CancellationToken cancellationToken = default)
        {
            var addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, new QueueSchemaAndCatalogOptions());
            var tableCache = new TableBasedQueueCache(
                (address, isStreamSupported) =>
                {
                    var canonicalAddress = addressTranslator.Parse(address);
                    return new SqlTableBasedQueue(sqlConstants, canonicalAddress, canonicalAddress.Address, isStreamSupported);
                },
                s => addressTranslator.Parse(s).Address,
                true);

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString);

            await CreateOutputQueueIfNecessary(addressTranslator, dbConnectionFactory, cancellationToken);

            await PurgeOutputQueue(addressTranslator, cancellationToken);

            dispatcher = new MessageDispatcher(s => addressTranslator.Parse(s).Address, new NoOpMulticastToUnicastConverter(), tableCache, null, dbConnectionFactory);
        }

        Task PurgeOutputQueue(QueueAddressTranslator addressParser, CancellationToken cancellationToken = default)
        {
            purger = new QueuePurger(dbConnectionFactory);
            var queueAddress = addressParser.Parse(ValidAddress);
            queue = new SqlTableBasedQueue(sqlConstants, queueAddress, queueAddress.Address, true);

            return purger.Purge(queue, cancellationToken);
        }

        Task CreateOutputQueueIfNecessary(QueueAddressTranslator addressParser, SqlServerDbConnectionFactory dbConnectionFactory, CancellationToken cancellationToken = default)
        {
            var queueCreator = new QueueCreator(sqlConstants, dbConnectionFactory, addressParser.Parse);

            return queueCreator.CreateQueueIfNecessary(new[] { ValidAddress }, new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"), cancellationToken);
        }

        QueuePurger purger;
        MessageDispatcher dispatcher;
        TableBasedQueue queue;
        SqlServerDbConnectionFactory dbConnectionFactory;

        const string ValidAddress = "TTBRTests";

        class NoOpMulticastToUnicastConverter : IMulticastToUnicastConverter
        {
            public Task<List<UnicastTransportOperation>> Convert(MulticastTransportOperation transportOperation, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new List<UnicastTransportOperation>());
            }
        }

    }
}