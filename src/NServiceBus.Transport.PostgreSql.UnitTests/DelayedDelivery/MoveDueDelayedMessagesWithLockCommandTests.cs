namespace NServiceBus.Transport.PostgreSql.UnitTests.DelayedDelivery;

using System;
using Npgsql;
using NUnit.Framework;

public class MoveDueDelayedMessagesWithLockCommandTests
{
    [TestCase(1000, 1000)]
    [TestCase(1000.1, 1001)]
    [TestCase(0.5, 1)]
    public void Populates_parameters_with_lock_retry_interval_rounded_up_to_whole_milliseconds(double lockRetryIntervalMs, int expectedLockRetryIntervalMs)
    {
        var sqlConstants = new PostgreSqlConstants();
        var moveDueCommand = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, "\"public\".\"delayed\"", "\"public\".\"input\"", TimeSpan.FromMilliseconds(lockRetryIntervalMs));
        using var command = new NpgsqlCommand();

        moveDueCommand.Populate(command, 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.CommandText, Is.EqualTo(string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, "\"public\".\"delayed\"", "\"public\".\"input\"")));
            Assert.That(command.Parameters["BatchSize"].Value, Is.EqualTo(42));
            Assert.That(command.Parameters["LockRetryIntervalMs"].Value, Is.EqualTo(expectedLockRetryIntervalMs));
        }
    }
}
