namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using NUnit.Framework;
    using Performance.TimeToBeReceived;
    using Routing;
    using SqlServer;
    using Transport;

    public class When_using_ttbr
    {
        ISqlConstants sqlConstants = new SqlServerConstants();

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
            var addressParser = new QueueAddressTranslator("nservicebus", "dbo", null, new QueueSchemaAndCatalogOptions());
            var tableCache = new TableBasedQueueCache(sqlConstants, addressParser, true);

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

            dbConnectionFactory = DbConnectionFactory.Default(connectionString);

            await CreateOutputQueueIfNecessary(addressParser, dbConnectionFactory, cancellationToken);

            await PurgeOutputQueue(addressParser, cancellationToken);

            dispatcher = new MessageDispatcher(addressParser, new NoOpMulticastToUnicastConverter(), tableCache, null, dbConnectionFactory);
        }

        Task PurgeOutputQueue(QueueAddressTranslator addressParser, CancellationToken cancellationToken = default)
        {
            purger = new QueuePurger(dbConnectionFactory);
            var queueAddress = addressParser.Parse(ValidAddress);
            queue = new TableBasedQueue(sqlConstants, queueAddress.QualifiedTableName, queueAddress.Address, true);

            return purger.Purge(queue, cancellationToken);
        }

        Task CreateOutputQueueIfNecessary(QueueAddressTranslator addressParser, DbConnectionFactory dbConnectionFactory, CancellationToken cancellationToken = default)
        {
            var queueCreator = new QueueCreator(sqlConstants, dbConnectionFactory, addressParser);

            return queueCreator.CreateQueueIfNecessary(new[] { ValidAddress }, new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"), cancellationToken);
        }

        QueuePurger purger;
        MessageDispatcher dispatcher;
        TableBasedQueue queue;
        DbConnectionFactory dbConnectionFactory;

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