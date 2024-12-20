namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Data.SqlClient;
    using NServiceBus.Transport.Sql.Shared;
    using NUnit.Framework;
    using Routing;
    using SqlServer;
    using Transport;
    using Unicast.Queuing;

    using SettingsKeys = SettingsKeys;


    public class When_dispatching_messages
    {
        SqlServerConstants sqlConstants = new();

        [TestCase(typeof(SendOnlyContextProvider), DispatchConsistency.Default)]
        [TestCase(typeof(HandlerContextProvider), DispatchConsistency.Default)]
        [TestCase(typeof(SendOnlyContextProvider), DispatchConsistency.Isolated)]
        [TestCase(typeof(HandlerContextProvider), DispatchConsistency.Isolated)]
        public async Task Outgoing_operations_are_stored_in_destination_queue(Type contextProviderType, DispatchConsistency dispatchConsistency)
        {
            using (var contextProvider = CreateContext(contextProviderType, dbConnectionFactory))
            {
                var operations = new TransportOperations(
                    CreateTransportOperation(id: "1", destination: ValidAddress, consistency: dispatchConsistency),
                    CreateTransportOperation(id: "2", destination: ValidAddress, consistency: dispatchConsistency)
                    );

                await dispatcher.Dispatch(operations, contextProvider.TransportTransaction);

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
            using (var contextProvider = CreateContext(contextProviderType, dbConnectionFactory))
            {
                var invalidOperations = new TransportOperations(
                    CreateTransportOperation(id: "3", destination: ValidAddress, consistency: dispatchConsistency),
                    CreateTransportOperation(id: "4", destination: InvalidAddress, consistency: dispatchConsistency)
                    );

                Assert.ThrowsAsync(Is.AssignableTo<Exception>(), async () =>
                {
                    await dispatcher.Dispatch(invalidOperations, contextProvider.TransportTransaction);
                    contextProvider.Complete();
                });
            }

            var messagesSent = await purger.Purge(queue);

            Assert.AreEqual(0, messagesSent);
        }

        [Test]
        public void Proper_exception_is_thrown_if_queue_does_not_exist()
        {
            var operation = new TransportOperation(new OutgoingMessage("1", [], new byte[0]), new UnicastAddressTag("InvalidQueue"));
            Assert.That(async () => await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction()), Throws.TypeOf<QueueNotFoundException>());
        }

        static TransportOperation CreateTransportOperation(string id, string destination, DispatchConsistency consistency)
        {
            return new TransportOperation(
                new OutgoingMessage(id, [], new byte[0]),
                new UnicastAddressTag(destination),
                requiredDispatchConsistency: consistency
                );
        }

        static IContextProvider CreateContext(Type contextType, SqlServerDbConnectionFactory dbConnectionFactory)
        {
            return contextType == typeof(SendOnlyContextProvider) ? new SendOnlyContextProvider() : new HandlerContextProvider(dbConnectionFactory);
        }

        [SetUp]
        public void Prepare()
        {
            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString);

            PrepareAsync().GetAwaiter().GetResult();
        }

        async Task PrepareAsync(CancellationToken cancellationToken = default)
        {
            var addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, null);
            var tableCache = new TableBasedQueueCache(
                (address, isStreamSupported) =>
                {
                    var canonicalAddress = addressTranslator.Parse(address);
                    return new SqlTableBasedQueue(sqlConstants, canonicalAddress, canonicalAddress.Address, isStreamSupported);
                },
                s => addressTranslator.Parse(s).Address,
                true);

            await CreateOutputQueueIfNecessary(addressTranslator, dbConnectionFactory, cancellationToken);

            await PurgeOutputQueue(addressTranslator, cancellationToken);

            dispatcher = new MessageDispatcher(s => addressTranslator.Parse(s).Address, new NoOpMulticastToUnicastConverter(), tableCache, null, dbConnectionFactory);
        }

        Task PurgeOutputQueue(QueueAddressTranslator addressTranslator, CancellationToken cancellationToken = default)
        {
            purger = new QueuePurger(dbConnectionFactory);
            var queueAddress = addressTranslator.Parse(ValidAddress);
            queue = new SqlTableBasedQueue(sqlConstants, queueAddress, ValidAddress, true);

            return purger.Purge(queue, cancellationToken);
        }

        Task CreateOutputQueueIfNecessary(QueueAddressTranslator addressTranslator, SqlServerDbConnectionFactory dbConnectionFactory, CancellationToken cancellationToken = default)
        {
            var queueCreator = new QueueCreator(sqlConstants, dbConnectionFactory, addressTranslator.Parse);

            return queueCreator.CreateQueueIfNecessary(new[] { ValidAddress }, new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"), cancellationToken);
        }

        QueuePurger purger;
        MessageDispatcher dispatcher;
        TableBasedQueue queue;
        const string ValidAddress = "TableBasedQueueDispatcherTests";
        const string InvalidAddress = "TableBasedQueueDispatcherTests.Invalid";

        SqlServerDbConnectionFactory dbConnectionFactory;

        class NoOpMulticastToUnicastConverter : IMulticastToUnicastConverter
        {
            public Task<List<UnicastTransportOperation>> Convert(MulticastTransportOperation transportOperation, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new List<UnicastTransportOperation>());
            }
        }

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
            public HandlerContextProvider(SqlServerDbConnectionFactory dbConnectionFactory)
            {
                sqlConnection = (SqlConnection)dbConnectionFactory.OpenNewConnection().GetAwaiter().GetResult();
                sqlTransaction = sqlConnection.BeginTransaction();

                TransportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, sqlConnection);
                TransportTransaction.Set(SettingsKeys.TransportTransactionSqlTransactionKey, sqlTransaction);

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