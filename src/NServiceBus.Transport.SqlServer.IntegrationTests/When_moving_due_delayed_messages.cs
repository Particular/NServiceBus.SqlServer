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

            await MoveWithoutTransaction(table, 10, CancellationToken);

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
            var command = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, delayedTable, inputTable.QualifiedTableName);
            var table = new DelayedMessageTable(sqlConstants, delayedTable.QualifiedTableName, command);

            using (var holder = await OpenTransaction(CancellationToken))
            {
                Assert.That(await TryGetAppLock(holder.Connection, holder.Transaction, command.LockResource, CancellationToken), Is.GreaterThanOrEqualTo(0));

                var result = await MoveWithoutTransaction(table, 10, CancellationToken);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(result, Is.EqualTo(new MoveDueResult.LockNotAcquired()));
                    Assert.That(await Count(inputTable, CancellationToken), Is.Zero, "The second mover should not move any messages while the lock is held");
                }

                holder.Transaction.Commit();
            }

            await MoveWithoutTransaction(table, 10, CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await Count(delayedTable, CancellationToken), Is.Zero);
                Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(4));
            }
        }

        [Test]
        public async Task Cancelled_move_releases_the_lock()
        {
            await StoreDueMessages(1, CancellationToken);
            var command = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, delayedTable, inputTable.QualifiedTableName);
            var table = new DelayedMessageTable(sqlConstants, delayedTable.QualifiedTableName, command);

            using var blocker = await OpenUnpooledConnection(CancellationToken);
            using var blockerTransaction = blocker.BeginTransaction();
            try
            {
                using (var blockCommand = blocker.CreateCommand())
                {
                    blockCommand.Transaction = blockerTransaction;
                    blockCommand.CommandText = $"SELECT TOP 0 * FROM {inputTable.QualifiedTableName} WITH (TABLOCKX, HOLDLOCK)";
                    await blockCommand.ExecuteNonQueryAsync(CancellationToken);
                }

                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                var moveConnection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(CancellationToken);
                var move = table.MoveDueMessages(10, moveConnection, null, cancellation.Token);

                var lockHeld = false;
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!lockHeld && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(100, CancellationToken);
                    lockHeld = await TryLockOnUnpooledConnection(command.LockResource, CancellationToken) == -1;
                }

                Assert.That(lockHeld && !move.IsCompleted, Is.True, "The move should hold the lock while it waits for the input table");

                cancellation.Cancel();
                try
                {
                    await move;
                }
                catch (Exception)
                {
                    // The cancelled move is expected to fail
                }

                moveConnection.Dispose();

                Assert.That(await TryLockOnUnpooledConnection(command.LockResource, CancellationToken), Is.GreaterThanOrEqualTo(0), "The cancelled move should have released the lock");
            }
            finally
            {
                blockerTransaction.Rollback();
                // A leaked lock would stay open on the pooled connection and affect later tests
                SqlConnection.ClearAllPools();
            }
        }

        [Test]
        public async Task Movers_on_different_connection_catalogs_exclude_each_other()
        {
            await StoreDueMessages(2, CancellationToken);
            var command = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, delayedTable, inputTable.QualifiedTableName);
            var table = new DelayedMessageTable(sqlConstants, delayedTable.QualifiedTableName, command);

            using var blocker = await OpenUnpooledConnection(CancellationToken);
            using var blockerTransaction = blocker.BeginTransaction();
            Task<MoveDueResult> moverA = null;
            SqlConnection moverAConnection = null;
            try
            {
                using (var blockCommand = blocker.CreateCommand())
                {
                    blockCommand.Transaction = blockerTransaction;
                    blockCommand.CommandText = $"SELECT TOP 0 * FROM {inputTable.QualifiedTableName} WITH (TABLOCKX, HOLDLOCK)";
                    await blockCommand.ExecuteNonQueryAsync(CancellationToken);
                }

                var otherCatalogConnectionString = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "nservicebus1", Pooling = false }.ConnectionString;
                moverAConnection = new SqlConnection(otherCatalogConnectionString);
                await moverAConnection.OpenAsync(CancellationToken);
                moverA = table.MoveDueMessages(10, moverAConnection, null, CancellationToken);

                var lockHeld = false;
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!lockHeld && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(100, CancellationToken);
                    lockHeld = await TryLockOnUnpooledConnection(command.LockResource, CancellationToken) == -1;
                }

                Assert.That(lockHeld && !moverA.IsCompleted, Is.True, "Mover A should hold the lock while it waits for the input table");

                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                cancellation.CancelAfter(TimeSpan.FromSeconds(5));
                var moverB = await MoveWithoutTransaction(table, 10, cancellation.Token);

                Assert.That(moverB, Is.EqualTo(new MoveDueResult.LockNotAcquired()));
            }
            finally
            {
                blockerTransaction.Rollback();
                try
                {
                    if (moverA != null)
                    {
                        await moverA;
                    }
                }
                finally
                {
                    moverAConnection?.Dispose();
                    // A leaked lock would stay open on the pooled connection and affect later tests
                    SqlConnection.ClearAllPools();
                }
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await Count(delayedTable, CancellationToken), Is.Zero);
                Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(2));
            }
        }

        [Test]
        public async Task Locked_move_leaves_XACT_ABORT_off_on_the_pooled_connection()
        {
            await StoreDueMessages(1, CancellationToken);
            var table = CreateLockedTable();

            int firstSpid;
            using (var connection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(CancellationToken))
            {
                firstSpid = await ExecuteScalarInt(connection, "SELECT CAST(@@SPID AS int)", CancellationToken);
                await table.MoveDueMessages(10, connection, null, CancellationToken);
            }

            using (var connection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(CancellationToken))
            {
                var secondSpid = await ExecuteScalarInt(connection, "SELECT CAST(@@SPID AS int)", CancellationToken);
                if (secondSpid != firstSpid)
                {
                    Assert.Inconclusive("The pool returned a different connection");
                }

                Assert.That(await ExecuteScalarInt(connection, "SELECT CAST(@@OPTIONS & 16384 AS int)", CancellationToken), Is.Zero);
            }
        }

        [Test]
        public async Task Failed_locked_move_leaves_no_transaction_or_lock_on_the_connection()
        {
            await StoreDueMessages(1, CancellationToken);
            var missingInputTable = new CanonicalQueueAddress(InputQueue + ".Missing", "dbo", "nservicebus");
            var command = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, delayedTable, missingInputTable.QualifiedTableName);
            var table = new DelayedMessageTable(sqlConstants, delayedTable.QualifiedTableName, command);

            try
            {
                using var connection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(CancellationToken);

                Assert.ThrowsAsync<SqlException>(() => table.MoveDueMessages(10, connection, null, CancellationToken));

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(await ExecuteScalarInt(connection, "SELECT @@TRANCOUNT", CancellationToken), Is.Zero);
                    Assert.That(await TryLockOnUnpooledConnection(command.LockResource, CancellationToken), Is.GreaterThanOrEqualTo(0));
                }
            }
            finally
            {
                // A leaked lock would stay open on the pooled connection and affect later tests
                SqlConnection.ClearAllPools();
            }
        }

        [Test]
        public async Task Locked_move_commits_on_a_session_with_implicit_transactions()
        {
            await StoreDueMessages(2, CancellationToken);
            var table = CreateLockedTable();

            try
            {
                using var connection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(CancellationToken);
                using (var setCommand = connection.CreateCommand())
                {
                    setCommand.CommandText = "SET IMPLICIT_TRANSACTIONS ON";
                    await setCommand.ExecuteNonQueryAsync(CancellationToken);
                }

                await table.MoveDueMessages(10, connection, null, CancellationToken);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(await ExecuteScalarInt(connection, "SELECT @@TRANCOUNT", CancellationToken), Is.Zero);
                    Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(2));
                }
            }
            finally
            {
                // A leaked lock would stay open on the pooled connection and affect later tests
                SqlConnection.ClearAllPools();
            }
        }

        DelayedMessageTable CreateUnlockedTable() =>
            new(sqlConstants, delayedTable.QualifiedTableName,
                new MoveDueDelayedMessagesCommand(sqlConstants, delayedTable.QualifiedTableName, inputTable.QualifiedTableName));

        DelayedMessageTable CreateLockedTable() =>
            new(sqlConstants, delayedTable.QualifiedTableName,
                new MoveDueDelayedMessagesWithLockCommand(sqlConstants, delayedTable, inputTable.QualifiedTableName));

        async Task<MoveDueResult> MoveInTransaction(DelayedMessageTable table, int batchSize, CancellationToken cancellationToken)
        {
            using var tx = await OpenTransaction(cancellationToken);
            var result = await table.MoveDueMessages(batchSize, tx.Connection, tx.Transaction, cancellationToken);
            tx.Transaction.Commit();
            return result;
        }

        async Task<MoveDueResult> MoveWithoutTransaction(DelayedMessageTable table, int batchSize, CancellationToken cancellationToken)
        {
            using var connection = (SqlConnection)await dbConnectionFactory.OpenNewConnection(cancellationToken);
            return await table.MoveDueMessages(batchSize, connection, null, cancellationToken);
        }

        async Task<SqlConnection> OpenUnpooledConnection(CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(new SqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        async Task<int> TryLockOnUnpooledConnection(string lockResource, CancellationToken cancellationToken)
        {
            using var connection = await OpenUnpooledConnection(cancellationToken);
            using var transaction = connection.BeginTransaction();
            var result = await TryGetAppLock(connection, transaction, lockResource, cancellationToken);
            transaction.Rollback();
            return result;
        }

        async Task<int> TryGetAppLock(SqlConnection connection, SqlTransaction transaction, string lockResource, CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DECLARE @result int; EXEC @result = {delayedTable.QuotedCatalogName}.sys.sp_getapplock @Resource = @r, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0; SELECT @result;";
            command.Parameters.AddWithValue("r", lockResource);
            return (int)await command.ExecuteScalarAsync(cancellationToken);
        }

        static async Task<int> ExecuteScalarInt(SqlConnection connection, string commandText, CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            return (int)await command.ExecuteScalarAsync(cancellationToken);
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
            connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";
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

        const string InputQueue = "MoveDueDelayedMessagesTests";

        readonly SqlServerConstants sqlConstants = new();
        string connectionString;
        SqlServerDbConnectionFactory dbConnectionFactory;
        CanonicalQueueAddress inputTable;
        CanonicalQueueAddress delayedTable;
    }
}
