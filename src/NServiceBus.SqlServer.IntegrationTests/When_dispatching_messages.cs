// ReSharper disable RedundantArgumentNameForLiteralExpression
// ReSharper disable RedundantArgumentName

namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Extensibility;
    using NUnit.Framework;
    using Routing;
    using Transport;
    using Transport.SQLServer;
    using Unicast.Queuing;

    public class When_dispatching_messages
    {
        [TestCase(typeof(SendOnlyContextProvider), DispatchConsistency.Default)]
        [TestCase(typeof(HandlerContextProvider), DispatchConsistency.Default)]
        [TestCase(typeof(SendOnlyContextProvider), DispatchConsistency.Isolated)]
        [TestCase(typeof(HandlerContextProvider), DispatchConsistency.Isolated)]
        public async Task Outgoing_operations_are_stored_in_destination_queue(Type contextProviderType, DispatchConsistency dispatchConsistency)
        {
            using (var contextProvider = CreateContext(contextProviderType))
            {
                var operations = new TransportOperations(
                    CreateTransportOperation(id: "1", destination: validAddress, consistency: dispatchConsistency),
                    CreateTransportOperation(id: "2", destination: validAddress, consistency: dispatchConsistency)
                    );

                await dispatcher.Dispatch(operations, contextProvider.TransportTransaction, contextProvider.Context);

                contextProvider.Complete();

                var messagesSent = await purger.Purge(queue);

                Assert.AreEqual(2, messagesSent);
            }
        }

        [TestCase(typeof(SendOnlyContextProvider), DispatchConsistency.Default)]
        [TestCase(typeof(HandlerContextProvider), DispatchConsistency.Default)]
        [TestCase(typeof(SendOnlyContextProvider), DispatchConsistency.Isolated)]
        [TestCase(typeof(HandlerContextProvider), DispatchConsistency.Isolated)]
        public async Task Outgoing_operations_are_stored_atomically(Type contextProviderType, DispatchConsistency dispatchConsistency)
        {
            using (var contextProvider = CreateContext(contextProviderType))
            {
                var invalidOperations = new TransportOperations(
                    CreateTransportOperation(id: "3", destination: validAddress, consistency: dispatchConsistency),
                    CreateTransportOperation(id: "4", destination: invalidAddress, consistency: dispatchConsistency)
                    );

                Assert.ThrowsAsync(Is.AssignableTo<Exception>(), async () =>
                {
                    await dispatcher.Dispatch(invalidOperations, contextProvider.TransportTransaction, contextProvider.Context);
                    contextProvider.Complete();
                });
            }

            var messagesSent = await purger.Purge(queue);

            Assert.AreEqual(0, messagesSent);
        }

        [Test]
        public void Proper_exception_is_thrown_if_queue_does_not_exist()
        {
            var operation = new TransportOperation(new OutgoingMessage("1", new Dictionary<string, string>(), new byte[0]), new UnicastAddressTag("InvalidQueue"));
            Assert.That(async () => await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction(), new ContextBag()), Throws.TypeOf<QueueNotFoundException>());
        }

        static TransportOperation CreateTransportOperation(string id, string destination, DispatchConsistency consistency)
        {
            return new TransportOperation(
                new OutgoingMessage(id, new Dictionary<string, string>(), new byte[0]),
                new UnicastAddressTag(destination),
                consistency
                );
        }

        static IContextProvider CreateContext(Type contextType)
        {
            return contextType == typeof(SendOnlyContextProvider) ? new SendOnlyContextProvider() : new HandlerContextProvider();
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
        const string validAddress = "TableBasedQueueDispatcherTests";
        const string invalidAddress = "TableBasedQueueDispatcherTests.Invalid";

        static SqlConnectionFactory sqlConnectionFactory = SqlConnectionFactory.Default(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");

        interface IContextProvider : IDisposable
        {
            ContextBag Context { get; }
            TransportTransaction TransportTransaction { get; }
            void Complete();
        }

        class SendOnlyContextProvider : IContextProvider
        {
            public virtual void Dispose()
            {
            }

            public ContextBag Context { get; } = new ContextBag();
            public TransportTransaction TransportTransaction { get; } = new TransportTransaction();

            public virtual void Complete()
            {
            }
        }

        class HandlerContextProvider : SendOnlyContextProvider
        {
            public HandlerContextProvider()
            {
                sqlConnection = sqlConnectionFactory.OpenNewConnection().GetAwaiter().GetResult();
                sqlTransaction = sqlConnection.BeginTransaction();

                TransportTransaction.Set(sqlConnection);
                TransportTransaction.Set(sqlTransaction);

                Context.Set(TransportTransaction);
            }

            public override void Dispose()
            {
                sqlTransaction.Dispose();
                sqlConnection.Dispose();
            }

            public override void Complete()
            {
                sqlTransaction.Commit();
            }

            SqlTransaction sqlTransaction;
            SqlConnection sqlConnection;
        }
    }
}