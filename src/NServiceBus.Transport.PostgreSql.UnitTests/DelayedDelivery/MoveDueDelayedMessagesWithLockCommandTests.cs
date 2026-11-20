namespace NServiceBus.Transport.PostgreSql.UnitTests.DelayedDelivery;

using Npgsql;
using NUnit.Framework;

public class MoveDueDelayedMessagesWithLockCommandTests
{
    [Test]
    public void Populates_command_with_only_the_batch_size_parameter()
    {
        var sqlConstants = new PostgreSqlConstants();
        var moveDueCommand = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, "\"public\".\"delayed\"", "\"public\".\"input\"");
        using var command = new NpgsqlCommand();

        moveDueCommand.Populate(command, 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.CommandText, Is.EqualTo(string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, "\"public\".\"delayed\"", "\"public\".\"input\"")));
            Assert.That(command.Parameters["BatchSize"].Value, Is.EqualTo(42));
            Assert.That(command.Parameters.Count, Is.EqualTo(1));
        }
    }
}
