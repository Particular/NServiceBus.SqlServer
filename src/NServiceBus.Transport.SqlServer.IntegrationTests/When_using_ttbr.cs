// ReSharper disable RedundantArgumentNameForLiteralExpression
// ReSharper disable RedundantArgumentName

namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DeliveryConstraints;
    using Extensibility;
    using NUnit.Framework;
    using Performance.TimeToBeReceived;
    using Routing;
    using Transport;
    using Transport.SqlServer;

    public class When_using_ttbr
    {
        [Test]
        public async Task Defaults_to_no_ttbr()
        {
            using (var connection = sqlConnectionFactory.OpenNewConnection().GetAwaiter().GetResult())
            {
                using (var transaction = connection.BeginTransaction())
                {
                    var transportTransaction = new TransportTransaction();

                    transportTransaction.Set(connection);
                    transportTransaction.Set(transaction);

                    var context = new ContextBag();
                    context.Set(transportTransaction);

                    var operation = new TransportOperation(
                        new OutgoingMessage("1", new Dictionary<string, string>(), new byte[0]),
                        new UnicastAddressTag(validAddress)
                    );

                    await dispatcher.Dispatch(new TransportOperations(operation), transportTransaction, context).ConfigureAwait(false);
                    transaction.Commit();
                }

                var message = await queue.TryReceive(connection, null).ConfigureAwait(false);
                Assert.IsFalse(message.Message.Expired);
            }
        }

        [Test]
        public async Task Diagnostic_headers_are_ignored()
        {
            using (var connection = sqlConnectionFactory.OpenNewConnection().GetAwaiter().GetResult())
            {
                using (var transaction = connection.BeginTransaction())
                {
                    var transportTransaction = new TransportTransaction();

                    transportTransaction.Set(connection);
                    transportTransaction.Set(transaction);

                    var context = new ContextBag();
                    context.Set(transportTransaction);

                    var headers = new Dictionary<string, string>
                    {
                        [Headers.TimeToBeReceived] = TimeSpan.FromMinutes(-1).ToString()
                    };
                    var operation = new TransportOperation(
                        new OutgoingMessage("1", headers, new byte[0]),
                        new UnicastAddressTag(validAddress)
                    );

                    await dispatcher.Dispatch(new TransportOperations(operation), transportTransaction, context).ConfigureAwait(false);
                    transaction.Commit();
                }

                var message = await queue.TryReceive(connection, null).ConfigureAwait(false);
                Assert.IsFalse(message.Message.Expired);
            }
        }

        [Test]
        public async Task Delivery_constraint_is_respected()
        {
            using (var connection = sqlConnectionFactory.OpenNewConnection().GetAwaiter().GetResult())
            {
                using (var transaction = connection.BeginTransaction())
                {
                    var transportTransaction = new TransportTransaction();

                    transportTransaction.Set(connection);
                    transportTransaction.Set(transaction);

                    var context = new ContextBag();
                    context.Set(transportTransaction);

                    var operation = new TransportOperation(
                        new OutgoingMessage("1", new Dictionary<string, string>(), new byte[0]),
                        new UnicastAddressTag(validAddress),
                        DispatchConsistency.Default,
                        new List<DeliveryConstraint>
                        {
                            new DiscardIfNotReceivedBefore(TimeSpan.FromMinutes(-1)) //Discard immediately
                        }
                    );

                    await dispatcher.Dispatch(new TransportOperations(operation), transportTransaction, context).ConfigureAwait(false);
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

        async Task PrepareAsync()
        {
            var addressParser = new QueueAddressTranslator("nservicebus", "dbo", null, new QueueSchemaAndCatalogSettings());
            var tableCache = new TableBasedQueueCache(addressParser);

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
            }

            sqlConnectionFactory = SqlConnectionFactory.Default(connectionString);

            await CreateOutputQueueIfNecessary(addressParser, sqlConnectionFactory);

            await PurgeOutputQueue(addressParser);

            dispatcher = new MessageDispatcher(addressParser, new NoOpMulticastToUnicastConverter(), tableCache, null, sqlConnectionFactory);
        }

        Task PurgeOutputQueue(QueueAddressTranslator addressParser)
        {
            purger = new QueuePurger(sqlConnectionFactory);
            var queueAddress = addressParser.Parse(validAddress);
            queue = new TableBasedQueue(queueAddress.QualifiedTableName, queueAddress.Address);

            return purger.Purge(queue);
        }

        static Task CreateOutputQueueIfNecessary(QueueAddressTranslator addressParser, SqlConnectionFactory sqlConnectionFactory)
        {
            var queueCreator = new QueueCreator(sqlConnectionFactory, addressParser, new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"));
            var queueBindings = new QueueBindings();
            queueBindings.BindReceiving(validAddress);

            return queueCreator.CreateQueueIfNecessary(queueBindings, "");
        }

        QueuePurger purger;
        MessageDispatcher dispatcher;
        TableBasedQueue queue;
        SqlConnectionFactory sqlConnectionFactory;

        const string validAddress = "TTBRTests";

        class NoOpMulticastToUnicastConverter : IMulticastToUnicastConverter
        {
            public Task<List<UnicastTransportOperation>> Convert(MulticastTransportOperation transportOperation)
            {
                return Task.FromResult(new List<UnicastTransportOperation>());
            }
        }

    }
}