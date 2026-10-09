namespace NServiceBus.Transport.SqlServer.UnitTests.DelayedDelivery;

using System;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

public class MoveDueDelayedMessagesWithLockCommandTests
{
    [TestCase(1000, 1000)]
    [TestCase(1000.1, 1001)]
    [TestCase(0.5, 1)]
    public void Populates_parameters_with_lock_delay_rounded_up_to_whole_milliseconds(double lockDelayMs, int expectedLockDelayMs)
    {
        var sqlConstants = new SqlServerConstants();
        var moveDueCommand = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, "[delayed]", "[input]", TimeSpan.FromMilliseconds(lockDelayMs));
        using var command = new SqlCommand();

        moveDueCommand.Populate(command, 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.CommandText, Is.EqualTo(string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, "[delayed]", "[input]")));
            Assert.That(command.Parameters["BatchSize"].Value, Is.EqualTo(42));
            Assert.That(command.Parameters["LockDelayMs"].Value, Is.EqualTo(expectedLockDelayMs));
        }
    }
}
