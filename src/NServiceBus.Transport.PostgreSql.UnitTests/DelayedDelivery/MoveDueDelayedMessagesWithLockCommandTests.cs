namespace NServiceBus.Transport.PostgreSql.UnitTests.DelayedDelivery;

using Npgsql;
using NUnit.Framework;

public class MoveDueDelayedMessagesWithLockCommandTests
{
    [Test]
    public void Populates_command_with_the_batch_size_and_lock_key_parameters()
    {
        var sqlConstants = new PostgreSqlConstants();
        var moveDueCommand = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, "\"public\".\"delayed\"", "\"public\".\"input\"");
        using var command = new NpgsqlCommand();

        moveDueCommand.Populate(command, 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.CommandText, Is.EqualTo(string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, "\"public\".\"delayed\"", "\"public\".\"input\"")));
            Assert.That(command.Parameters["BatchSize"].Value, Is.EqualTo(42));
            // Pinned because instances on different versions must derive the same key
            Assert.That(command.Parameters["MoverLockKey"].Value, Is.EqualTo(3093868108017798208L));
            Assert.That(command.Parameters.Count, Is.EqualTo(2));
        }
    }

    [Test]
    public void Owns_the_transaction() =>
        Assert.That(new MoveDueDelayedMessagesWithLockCommand(new PostgreSqlConstants(), "\"public\".\"delayed\"", "\"public\".\"input\"").OwnsTransaction, Is.True);

    [Test]
    public void Derives_a_different_lock_key_for_each_delayed_table()
    {
        var sqlConstants = new PostgreSqlConstants();
        using var first = new NpgsqlCommand();
        using var second = new NpgsqlCommand();

        new MoveDueDelayedMessagesWithLockCommand(sqlConstants, "\"public\".\"first\"", "\"public\".\"input\"").Populate(first, 42);
        new MoveDueDelayedMessagesWithLockCommand(sqlConstants, "\"public\".\"second\"", "\"public\".\"input\"").Populate(second, 42);

        Assert.That(first.Parameters["MoverLockKey"].Value, Is.Not.EqualTo(second.Parameters["MoverLockKey"].Value));
    }

    [Test]
    public void Keeps_the_table_name_out_of_string_literals()
    {
        var sqlConstants = new PostgreSqlConstants();
        var moveDueCommand = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, "\"public\".\"o'brien.delayed\"", "\"public\".\"input\"");
        using var command = new NpgsqlCommand();

        moveDueCommand.Populate(command, 42);

        Assert.That(command.CommandText, Does.Not.Contain("'\"public\".\"o'brien.delayed\"'"));
    }
}
