namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using NServiceBus.Transport.Sql.Shared;
    using NUnit.Framework;

    public class When_moving_due_delayed_messages
    {
        [Test]
        public async Task Moves_due_messages_without_lock_by_default()
        {
            await StoreDueMessages(3, CancellationToken);
            var table = CreateUnlockedTable();

            await MoveInTransaction(table, 10, CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await Count(delayedTable, CancellationToken), Is.Zero);
                Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(3));
            }
        }

        [Test]
        public async Task Concurrent_movers_both_move_without_lock()
        {
            await StoreDueMessages(4, CancellationToken);
            var table = CreateUnlockedTable();

            using (var first = await OpenTransaction(CancellationToken))
            {
                await table.MoveDueMessages(2, first.Connection, first.Transaction, CancellationToken);

                // READPAST lets the second mover skip the rows locked by the first
                await MoveInTransaction(table, 10, CancellationToken);

                Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(2));

                first.Transaction.Commit();
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await Count(delayedTable, CancellationToken), Is.Zero);
                Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(4));
            }
        }

        [Test]
        public async Task Moves_due_messages_with_lock_when_uncontended()
        {
            await StoreDueMessages(3, CancellationToken);
            var table = CreateLockedTable();

            await MoveInTransaction(table, 10, CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await Count(delayedTable, CancellationToken), Is.Zero);
                Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(3));
            }
        }

        [Test]
        public async Task Second_mover_skips_while_lock_is_held_and_rest_move_after_holder_commits()
        {
            await StoreDueMessages(4, CancellationToken);
            var table = CreateLockedTable();

            using (var holder = await OpenTransaction(CancellationToken))
            {
                await table.MoveDueMessages(2, holder.Connection, holder.Transaction, CancellationToken);

                var before = DateTime.UtcNow;
                var nextDue = await MoveInTransaction(table, 10, CancellationToken);
                var after = DateTime.UtcNow;

                Assert.That(await Count(inputTable, CancellationToken), Is.Zero, "The second mover should not move any messages while the lock is held");

                holder.Transaction.Commit();
            }

            Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(2));

            await MoveInTransaction(table, 10, CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await Count(delayedTable, CancellationToken), Is.Zero);
                Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(4));
            }
        }

        DelayedMessageTable CreateUnlockedTable() =>
            new(sqlConstants, delayedTable.QualifiedTableName,
                new MoveDueDelayedMessagesCommand(sqlConstants, delayedTable.QualifiedTableName, inputTable.QualifiedTableName));

        DelayedMessageTable CreateLockedTable() =>
            new(sqlConstants, delayedTable.QualifiedTableName,
                new MoveDueDelayedMessagesWithLockCommand(sqlConstants, delayedTable.QualifiedTableName, inputTable.QualifiedTableName, LockDelay));

        async Task<DateTime> MoveInTransaction(DelayedMessageTable table, int batchSize, CancellationToken cancellationToken)
        {
            using var tx = await OpenTransaction(cancellationToken);
            var nextDue = await table.MoveDueMessages(batchSize, tx.Connection, tx.Transaction, cancellationToken);
            tx.Transaction.Commit();
            return nextDue;
        }

        async Task<OpenTx> OpenTransaction(CancellationToken cancellationToken)
        {
            var connection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(cancellationToken);
            return new OpenTx(connection, connection.BeginTransaction());
        }

        async Task StoreDueMessages(int count, CancellationToken cancellationToken)
        {
            using var connection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(cancellationToken);
            for (var i = 0; i < count; i++)
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"INSERT INTO {delayedTable.QualifiedTableName} (Headers, Body, Due) VALUES (N'{{}}', 0x00, DATEADD(s, -10, GETUTCDATE()))";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        async Task<int> Count(CanonicalQueueAddress table, CancellationToken cancellationToken)
        {
            using var connection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table.QualifiedTableName} WITH (READPAST)";
            return (int)await command.ExecuteScalarAsync(cancellationToken);
        }

        async Task Purge(CanonicalQueueAddress table, CancellationToken cancellationToken)
        {
            using var connection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {table.QualifiedTableName}";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        [SetUp]
        public async Task Prepare()
        {
            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";
            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString);

            var addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, null);
            inputTable = addressTranslator.Parse(InputQueue);
            delayedTable = new CanonicalQueueAddress(InputQueue + ".Delayed", "dbo", "nservicebus");

            var queueCreator = new QueueCreator(sqlConstants, dbConnectionFactory, addressTranslator.Parse);
            await queueCreator.CreateQueueIfNecessary([InputQueue], delayedTable, CancellationToken);

            await Purge(inputTable, CancellationToken);
            await Purge(delayedTable, CancellationToken);
        }

        static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

        sealed record OpenTx(SqlConnection Connection, SqlTransaction Transaction) : IDisposable
        {
            public void Dispose()
            {
                Transaction.Dispose();
                Connection.Dispose();
            }
        }

        static readonly TimeSpan LockDelay = TimeSpan.FromSeconds(5);
        const string InputQueue = "MoveDueDelayedMessagesTests";

        readonly SqlServerConstants sqlConstants = new();
        SqlServerDbConnectionFactory dbConnectionFactory;
        CanonicalQueueAddress inputTable;
        CanonicalQueueAddress delayedTable;
    }
}
