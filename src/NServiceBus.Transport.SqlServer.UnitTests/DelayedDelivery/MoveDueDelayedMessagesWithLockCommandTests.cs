namespace NServiceBus.Transport.SqlServer.UnitTests.DelayedDelivery;

using Microsoft.Data.SqlClient;
using NUnit.Framework;

public class MoveDueDelayedMessagesWithLockCommandTests
{
    [Test]
    public void Populates_command_with_only_the_batch_size_parameter()
    {
        var sqlConstants = new SqlServerConstants();
        var moveDueCommand = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, "[delayed]", "[input]");
        using var command = new SqlCommand();

        moveDueCommand.Populate(command, 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.CommandText, Is.EqualTo(string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, "[delayed]", "[input]")));
            Assert.That(command.Parameters["BatchSize"].Value, Is.EqualTo(42));
            Assert.That(command.Parameters.Count, Is.EqualTo(1));
        }
    }
}
