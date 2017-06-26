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
    using Transport.SQLServer;

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

                var expired = await queue.PurgeBatchOfExpiredMessages(connection, 100).ConfigureAwait(false);

                Assert.AreEqual(0, expired);
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

                var expired = await queue.PurgeBatchOfExpiredMessages(connection, 100).ConfigureAwait(false);

                Assert.AreEqual(0, expired);
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

                var expired = await queue.PurgeBatchOfExpiredMessages(connection, 100).ConfigureAwait(false);

                Assert.AreEqual(1, expired);
            }
        }

        [SetUp]
        public void Prepare()
        {
            PrepareAsync().GetAwaiter().GetResult();
        }

        async Task PrepareAsync()
        {
            var addressParser = new QueueAddressParser("dbo", null, null);

            await CreateOutputQueueIfNecessary(addressParser);

            await PurgeOutputQueue(addressParser);

            dispatcher = new MessageDispatcher(new TableBasedQueueDispatcher(sqlConnectionFactory), addressParser);
        }

        Task PurgeOutputQueue(QueueAddressParser addressParser)
        {
            purger = new QueuePurger(sqlConnectionFactory);
            var queueAddress = addressParser.Parse(validAddress);
            queue = new TableBasedQueue(queueAddress);

            return purger.Purge(queue);
        }

        static Task CreateOutputQueueIfNecessary(QueueAddressParser addressParser)
        {
            var queueCreator = new QueueCreator(sqlConnectionFactory, addressParser);
            var queueBindings = new QueueBindings();
            queueBindings.BindReceiving(validAddress);

            return queueCreator.CreateQueueIfNecessary(queueBindings, "");
        }

        QueuePurger purger;
        MessageDispatcher dispatcher;
        TableBasedQueue queue;
        const string validAddress = "TTBRTests";

        static SqlConnectionFactory sqlConnectionFactory = SqlConnectionFactory.Default(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");
    }
}