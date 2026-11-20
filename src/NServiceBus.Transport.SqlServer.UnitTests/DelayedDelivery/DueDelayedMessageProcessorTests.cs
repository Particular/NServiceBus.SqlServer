namespace NServiceBus.Transport.SqlServer.UnitTests.DelayedDelivery;

using System;
using NUnit.Framework;
using Sql.Shared;

public class DueDelayedMessageProcessorTests
{
    [TestCase(1000, 500, 1000)]
    [TestCase(3, 1, 3)]
    [TestCase(1, 1, 1)]
    public void Lock_retry_delay_is_whole_milliseconds_between_half_and_the_maximum(long maxMilliseconds, long expectedMin, long expectedMax)
    {
        var random = new Random(42);

        for (var i = 0; i < 1000; i++)
        {
            var delay = DueDelayedMessageProcessor.NextLockRetryDelay(maxMilliseconds, random).TotalMilliseconds;

            Assert.That(delay, Is.InRange(expectedMin, expectedMax).And.EqualTo(Math.Truncate(delay)));
        }
    }

    [Test]
    public void Lock_retry_delay_varies_between_attempts()
    {
        var random = new Random(42);
        var first = DueDelayedMessageProcessor.NextLockRetryDelay(1000, random);

        var varies = false;
        for (var i = 0; i < 100 && !varies; i++)
        {
            varies = DueDelayedMessageProcessor.NextLockRetryDelay(1000, random) != first;
        }

        Assert.That(varies, Is.True);
    }
}
