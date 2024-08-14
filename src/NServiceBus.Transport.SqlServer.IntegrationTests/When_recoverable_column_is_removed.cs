namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using Microsoft.Data.SqlClient;
    using System.Threading.Tasks;
    using Extensibility;
    using NUnit.Framework;
    using Routing;
    using SqlServer;
    using System.Threading;
    using Sql.Shared.Queuing;
    using Sql.Shared.Receiving;
    using Sql.Shared.Sending;
    using SettingsKeys = SettingsKeys;

    public class When_recoverable_column_is_removed
    {
        SqlServerConstants sqlConstants = new();

        [TestCase(typeof(SendOnlyContextProvider), DispatchConsistency.Default)]
        [TestCase(typeof(HandlerContextProvider), DispatchConsistency.Default)]
        [TestCase(typeof(SendOnlyContextProvider), DispatchConsistency.Isolated)]
        [TestCase(typeof(HandlerContextProvider), DispatchConsistency.Isolated)]
        public async Task Should_recover(Type contextProviderType, DispatchConsistency dispatchConsistency)
        {
            // Setup

            var token = CancellationToken.None;

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";
            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString);

            var addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, null);
            var purger = new QueuePurger(dbConnectionFactory);

            await RemoveQueueIfPresent(QueueName, token);
            await RemoveQueueIfPresent($"{QueueName}.Delayed", token);
            await CreateOutputQueueIfNecessary(addressTranslator, dbConnectionFactory);

            var tableCache = new TableBasedQueueCache(
                (address, isStreamSupported) =>
                {
                    var canonicalAddress = addressTranslator.Parse(address);
                    return new SqlTableBasedQueue(sqlConstants, canonicalAddress.QualifiedTableName, canonicalAddress.Address, isStreamSupported);
                },
                s => addressTranslator.Parse(s).Address,
                true);
            var queue = tableCache.Get(QueueName);
            dispatcher = new MessageDispatcher(s => addressTranslator.Parse(s).Address, new NoOpMulticastToUnicastConverter(), tableCache, null, dbConnectionFactory);

            // Run normally
            int messagesSent = await RunTest(contextProviderType, dispatchConsistency, queue, purger, token);
            Assert.That(messagesSent, Is.EqualTo(1));

            // Remove Recoverable column
            await DropRecoverableColumn(token);

            var exception = Assert.ThrowsAsync<Exception>(() => RunTest(contextProviderType, dispatchConsistency, queue, purger, token));
            Assert.That(exception.Message, Does.Contain("change in the existence of the Recoverable column"));

            // Try again, should work
            int messagesSentAttempt2 = await RunTest(contextProviderType, dispatchConsistency, queue, purger, token);
            Assert.That(messagesSentAttempt2, Is.EqualTo(1));

            // Put the Recoverable column back
            await AddRecoverableColumn(token);

            var exception2 = Assert.ThrowsAsync<Exception>(() => RunTest(contextProviderType, dispatchConsistency, queue, purger, token));
            Assert.That(exception2.Message, Does.Contain("change in the existence of the Recoverable column"));

            // Try again, should work
            int messagesSentAttempt3 = await RunTest(contextProviderType, dispatchConsistency, queue, purger, token);
            Assert.That(messagesSentAttempt3, Is.EqualTo(1));
        }

        async Task<int> RunTest(Type contextProviderType, DispatchConsistency dispatchConsistency, TableBasedQueue queue, QueuePurger purger, CancellationToken cancellationToken)
        {
            using (var contextProvider = CreateContext(contextProviderType, dbConnectionFactory))
            {
                // Run with Recoverable column in place

                var operations = new TransportOperations(CreateTransportOperation(id: "1", destination: QueueName, consistency: dispatchConsistency));
                await dispatcher.Dispatch(operations, contextProvider.TransportTransaction, cancellationToken);
                contextProvider.Complete();

                return await purger.Purge(queue, cancellationToken);
            }
        }

        async Task DropRecoverableColumn(CancellationToken cancellationToken)
        {
            var cmdText = $"ALTER TABLE {QueueName} DROP COLUMN Recoverable";
            using (var connection = await dbConnectionFactory.OpenNewConnection(cancellationToken))
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = cmdText;
                cmd.CommandType = CommandType.Text;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        async Task AddRecoverableColumn(CancellationToken cancellationToken)
        {
            var cmdText = $"ALTER TABLE {QueueName} ADD Recoverable bit NOT NULL";
            using (var connection = await dbConnectionFactory.OpenNewConnection(cancellationToken))
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = cmdText;
                cmd.CommandType = CommandType.Text;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        async Task RemoveQueueIfPresent(string queueName, CancellationToken cancellationToken)
        {
            var cmdText = $@"
IF EXISTS (
    SELECT *
    FROM nservicebus.sys.objects
    WHERE object_id = OBJECT_ID(N'{queueName}')
        AND type in (N'U'))
BEGIN
    DROP TABLE nservicebus.dbo.[{queueName}]
END";
            using (var connection = await dbConnectionFactory.OpenNewConnection(cancellationToken))
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = cmdText;
                cmd.CommandType = CommandType.Text;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        static IContextProvider CreateContext(Type contextType, SqlServerDbConnectionFactory dbConnectionFactory)
        {
            return contextType == typeof(SendOnlyContextProvider) ? new SendOnlyContextProvider() : new HandlerContextProvider(dbConnectionFactory);
        }

        static TransportOperation CreateTransportOperation(string id, string destination, DispatchConsistency consistency)
        {
            return new TransportOperation(
                new OutgoingMessage(id, [], new byte[0]),
                new UnicastAddressTag(destination),
                requiredDispatchConsistency: consistency
                );
        }

        Task CreateOutputQueueIfNecessary(QueueAddressTranslator addressTranslator, SqlServerDbConnectionFactory dbConnectionFactory, CancellationToken cancellationToken = default)
        {
            var queueCreator = new QueueCreator(sqlConstants, dbConnectionFactory, addressTranslator.Parse);

            return queueCreator.CreateQueueIfNecessary(new[] { QueueName }, new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"), cancellationToken);
        }

        MessageDispatcher dispatcher;
        const string QueueName = "RecoverableColumnRemovalTable";

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
                sqlConnection = dbConnectionFactory.OpenNewConnection().GetAwaiter().GetResult();
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

            DbTransaction sqlTransaction;
            DbConnection sqlConnection;
        }
    }
}