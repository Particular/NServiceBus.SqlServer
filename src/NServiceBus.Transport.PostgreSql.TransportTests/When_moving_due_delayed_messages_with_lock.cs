namespace NServiceBus.Transport.PostgreSql.TransportTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NUnit.Framework;
using Sql.Shared;

public class When_moving_due_delayed_messages_with_lock
{
    [Test]
    public async Task Second_mover_skips_while_the_key_is_held_and_moves_after_release()
    {
        await StoreDueMessages(3, CancellationToken);
        var command = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, delayedTable.QualifiedTableName, inputTable.QualifiedTableName);
        var table = new DelayedMessageTable(sqlConstants, delayedTable.QualifiedTableName, command);

        await using (var holder = await OpenConnection(CancellationToken))
        await using (var holderTransaction = await holder.BeginTransactionAsync(CancellationToken))
        {
            Assert.That(await TryLock(holder, command.LockKey, CancellationToken), Is.True);
            Assert.That(await GrantedAdvisoryLocks(command.LockKey, CancellationToken), Is.EqualTo(1));

            var result = await Move(table, CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(new MoveDueResult.LockNotAcquired()));
                Assert.That(await Count(inputTable, CancellationToken), Is.Zero);
                Assert.That(await Count(delayedTable, CancellationToken), Is.EqualTo(3));
            }

            await holderTransaction.CommitAsync(CancellationToken);
        }

        Assert.That(await GrantedAdvisoryLocks(command.LockKey, CancellationToken), Is.Zero);

        var afterRelease = await Move(table, CancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterRelease, Is.InstanceOf<MoveDueResult.Moved>());
            Assert.That(await Count(delayedTable, CancellationToken), Is.Zero);
            Assert.That(await Count(inputTable, CancellationToken), Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Failed_insert_keeps_the_messages_and_frees_the_key()
    {
        await StoreDueMessages(2, CancellationToken);
        await AddInsertTrigger(failingInputTable, "RAISE EXCEPTION 'input rejected'", CancellationToken);
        var command = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, delayedTable.QualifiedTableName, failingInputTable.QualifiedTableName);
        var table = new DelayedMessageTable(sqlConstants, delayedTable.QualifiedTableName, command);

        // Kept open because returning it to the pool resets the session and would hide a leaked key
        await using var moveConnection = await OpenConnection(CancellationToken);
        var exception = Assert.ThrowsAsync<PostgresException>(() => table.MoveDueMessages(10, moveConnection, null, CancellationToken));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.MessageText, Is.EqualTo("input rejected"));
            Assert.That(await Count(delayedTable, CancellationToken), Is.EqualTo(2));
            Assert.That(await Count(failingInputTable, CancellationToken), Is.Zero);
            Assert.That(await GrantedAdvisoryLocks(command.LockKey, CancellationToken), Is.Zero);
            Assert.That(await TryLockOnNewConnection(command.LockKey, CancellationToken), Is.True);
        }
    }

    [Test]
    public async Task Cancelled_move_keeps_the_messages_and_frees_the_key()
    {
        await StoreDueMessages(2, CancellationToken);
        await AddInsertTrigger(slowInputTable, "PERFORM pg_sleep(30)", CancellationToken);
        var command = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, delayedTable.QualifiedTableName, slowInputTable.QualifiedTableName);
        var table = new DelayedMessageTable(sqlConstants, delayedTable.QualifiedTableName, command);

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        await using var moveConnection = await OpenConnection(CancellationToken);
        var move = table.MoveDueMessages(10, moveConnection, null, cancellation.Token);

        var lockHeld = false;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!lockHeld && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, CancellationToken);
            lockHeld = await GrantedAdvisoryLocks(command.LockKey, CancellationToken) == 1;
        }

        Assert.That(lockHeld && !move.IsCompleted, Is.True, "The move should hold the key while the insert trigger sleeps");

        await cancellation.CancelAsync();
        Assert.CatchAsync<Exception>(() => move);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Count(delayedTable, CancellationToken), Is.EqualTo(2));
            Assert.That(await Count(slowInputTable, CancellationToken), Is.Zero);
            Assert.That(await GrantedAdvisoryLocks(command.LockKey, CancellationToken), Is.Zero);
            Assert.That(await TryLockOnNewConnection(command.LockKey, CancellationToken), Is.True);
        }
    }

    static async Task<MoveDueResult> Move(DelayedMessageTable table, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnection(cancellationToken);
        return await table.MoveDueMessages(10, connection, null, cancellationToken);
    }

    static async Task<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(ConfigurePostgreSqlTransportInfrastructure.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    static async Task<bool> TryLock(NpgsqlConnection connection, long key, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(@key)", connection);
        command.Parameters.AddWithValue("key", key);
        return (bool)await command.ExecuteScalarAsync(cancellationToken);
    }

    static async Task<bool> TryLockOnNewConnection(long key, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnection(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var acquired = await TryLock(connection, key, cancellationToken);
        await transaction.RollbackAsync(cancellationToken);
        return acquired;
    }

    // A bigint advisory key shows up as classid (high 32 bits) and objid (low 32 bits) with objsubid 1
    static async Task<long> GrantedAdvisoryLocks(long key, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnection(cancellationToken);
        await using var command = new NpgsqlCommand(@"
SELECT count(*) FROM pg_locks
WHERE locktype = 'advisory' AND granted AND objsubid = 1
  AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
  AND ((classid::bigint << 32) | objid::bigint) = @key", connection);
        command.Parameters.AddWithValue("key", key);
        return (long)await command.ExecuteScalarAsync(cancellationToken);
    }

    static async Task StoreDueMessages(int count, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnection(cancellationToken);
        for (var i = 0; i < count; i++)
        {
            await using var command = new NpgsqlCommand($"INSERT INTO {delayedTable.QualifiedTableName} (Headers, Body, Due) VALUES ('{{}}', '\\x00', timestamptz (now() AT TIME ZONE 'UTC') - INTERVAL '10 seconds')", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    static async Task AddInsertTrigger(CanonicalQueueAddress table, string body, CancellationToken cancellationToken)
    {
        var function = PostgreSqlNameHelper.Quote(table.Table + "_trigger");
        await Execute($@"
CREATE OR REPLACE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN {body}; RETURN NEW; END $$;
DROP TRIGGER IF EXISTS {function} ON {table.QualifiedTableName};
CREATE TRIGGER {function} BEFORE INSERT ON {table.QualifiedTableName} FOR EACH ROW EXECUTE FUNCTION {function}();", cancellationToken);
    }

    static async Task<long> Count(CanonicalQueueAddress table, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnection(cancellationToken);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table.QualifiedTableName}", connection);
        return (long)await command.ExecuteScalarAsync(cancellationToken);
    }

    static async Task Execute(string sql, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnection(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    [SetUp]
    public async Task Prepare()
    {
        await Execute(string.Format(sqlConstants.CreateDelayedMessageStoreText, delayedTable.QualifiedTableName, delayedTable.Table), CancellationToken);
        foreach (var input in new[] { inputTable, failingInputTable, slowInputTable })
        {
            await Execute(string.Format(sqlConstants.CreateQueueText, input.QualifiedTableName), CancellationToken);
            await Execute($"DELETE FROM {input.QualifiedTableName}", CancellationToken);
        }

        await Execute($"DELETE FROM {delayedTable.QualifiedTableName}", CancellationToken);
    }

    static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    readonly PostgreSqlConstants sqlConstants = new();

    static readonly CanonicalQueueAddress delayedTable = new("MoverLockTests.Delayed", "public");
    static readonly CanonicalQueueAddress inputTable = new("MoverLockTests", "public");
    static readonly CanonicalQueueAddress failingInputTable = new("MoverLockTestsFailingInput", "public");
    static readonly CanonicalQueueAddress slowInputTable = new("MoverLockTestsSlowInput", "public");
}
