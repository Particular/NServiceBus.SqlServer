namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Sql.Shared;
    using NUnit.Framework;

    public class When_moving_due_delayed_messages
    {
        [Test]
        public async Task Moves_due_messages_when_no_other_instance_holds_the_lock()
        {
            await StoreDueMessages(3, CancellationToken);

            await MoveInTransaction(10, CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await Count(delayedTable, CancellationToken), Is.Zero);
                Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(3));
            }
        }

        [Test]
        public async Task Skips_the_table_while_another_instance_holds_the_lock()
        {
            await StoreDueMessages(4, CancellationToken);

            using (var holder = await OpenTransaction(CancellationToken))
            {
                await table.MoveDueMessages(2, holder.Connection, holder.Transaction, CancellationToken);

                var result = await MoveInTransaction(10, CancellationToken);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(result, Is.EqualTo(new MoveDueResult.LockNotAcquired()));
                    Assert.That(await Count(inputTable, CancellationToken), Is.Zero, "The second mover should not move any messages while the lock is held");
                }

                holder.Transaction.Commit();
            }

            Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(2));

            await MoveInTransaction(10, CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await Count(delayedTable, CancellationToken), Is.Zero);
                Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(4));
            }
        }

        async Task<MoveDueResult> MoveInTransaction(int batchSize, CancellationToken cancellationToken)
        {
            using var tx = await OpenTransaction(cancellationToken);
            var result = await table.MoveDueMessages(batchSize, tx.Connection, tx.Transaction, cancellationToken);
            tx.Transaction.Commit();
            return result;
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

        async Task<int> Count(CanonicalQueueAddress queue, CancellationToken cancellationToken)
        {
            using var connection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {queue.QualifiedTableName} WITH (READPAST)";
            return (int)await command.ExecuteScalarAsync(cancellationToken);
        }

        async Task Purge(CanonicalQueueAddress queue, CancellationToken cancellationToken)
        {
            using var connection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {queue.QualifiedTableName}";
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
            table = new DelayedMessageTable(sqlConstants, delayedTable.QualifiedTableName, inputTable.QualifiedTableName);

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

        const string InputQueue = "MoveDueDelayedMessagesTests";

        readonly SqlServerConstants sqlConstants = new();
        SqlServerDbConnectionFactory dbConnectionFactory;
        CanonicalQueueAddress inputTable;
        CanonicalQueueAddress delayedTable;
        DelayedMessageTable table;
    }
}
