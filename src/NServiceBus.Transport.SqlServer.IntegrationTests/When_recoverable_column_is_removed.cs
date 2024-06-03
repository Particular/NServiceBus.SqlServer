namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Data.SqlClient;
    using NUnit.Framework;
    using Routing;
    using SqlServer;
    using Transport;

    public class When_recoverable_column_is_removed
    {
        ISqlConstants sqlConstants = new SqlServerConstants();

        [TestCase(typeof(SendOnlyContextProvider), DispatchConsistency.Default)]
        [TestCase(typeof(HandlerContextProvider), DispatchConsistency.Default)]
        [TestCase(typeof(SendOnlyContextProvider), DispatchConsistency.Isolated)]
        [TestCase(typeof(HandlerContextProvider), DispatchConsistency.Isolated)]
        public async Task Should_recover(Type contextProviderType, DispatchConsistency dispatchConsistency)
        {
            // Setup

            var token = CancellationToken.None;

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";
            dbConnectionFactory = DbConnectionFactory.Default(connectionString);

            var addressParser = new QueueAddressTranslator("nservicebus", "dbo", null, null);
            var purger = new QueuePurger(dbConnectionFactory);

            await RemoveQueueIfPresent(QueueName, token);
            await RemoveQueueIfPresent($"{QueueName}.Delayed", token);
            await CreateOutputQueueIfNecessary(addressParser, dbConnectionFactory);

            var tableCache = new TableBasedQueueCache(sqlConstants, addressParser, true);
            var queue = tableCache.Get(QueueName);
            dispatcher = new MessageDispatcher(addressParser, new NoOpMulticastToUnicastConverter(), tableCache, null, dbConnectionFactory);

            // Run normally
            int messagesSent = await RunTest(contextProviderType, dispatchConsistency, queue, purger, token);
            Assert.AreEqual(1, messagesSent);

            // Remove Recoverable column
            await DropRecoverableColumn(token);

            var exception = Assert.ThrowsAsync<Exception>(() => RunTest(contextProviderType, dispatchConsistency, queue, purger, token));
            Assert.True(exception.Message.Contains("change in the existence of the Recoverable column"));

            // Try again, should work
            int messagesSentAttempt2 = await RunTest(contextProviderType, dispatchConsistency, queue, purger, token);
            Assert.AreEqual(1, messagesSentAttempt2);

            // Put the Recoverable column back
            await AddRecoverableColumn(token);

            var exception2 = Assert.ThrowsAsync<Exception>(() => RunTest(contextProviderType, dispatchConsistency, queue, purger, token));
            Assert.True(exception2.Message.Contains("change in the existence of the Recoverable column"));

            // Try again, should work
            int messagesSentAttempt3 = await RunTest(contextProviderType, dispatchConsistency, queue, purger, token);
            Assert.AreEqual(1, messagesSentAttempt3);
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

        static IContextProvider CreateContext(Type contextType, DbConnectionFactory dbConnectionFactory)
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

        Task CreateOutputQueueIfNecessary(QueueAddressTranslator addressTranslator, DbConnectionFactory dbConnectionFactory, CancellationToken cancellationToken = default)
        {
            var queueCreator = new QueueCreator(sqlConstants, dbConnectionFactory, addressTranslator);

            return queueCreator.CreateQueueIfNecessary(new[] { QueueName }, new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"), cancellationToken);
        }

        MessageDispatcher dispatcher;
        const string QueueName = "RecoverableColumnRemovalTable";

        DbConnectionFactory dbConnectionFactory;

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
            public HandlerContextProvider(DbConnectionFactory dbConnectionFactory)
            {
                //TODO: get rid of this cast
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