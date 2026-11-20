namespace NServiceBus.Transport.SqlServer.UnitTests.DelayedDelivery;

using System;
using NUnit.Framework;

public class DelayedDeliveryOptionsTests
{
    [Test]
    public void LockRetryInterval_defaults_to_one_second()
    {
        var options = new DelayedDeliveryOptions();

        Assert.That(options.LockRetryInterval, Is.EqualTo(TimeSpan.FromSeconds(1)));
    }

    [TestCase(1)]
    [TestCase(60_000)]
    public void LockRetryInterval_accepts_values_from_above_zero_up_to_one_minute(int milliseconds)
    {
        var options = new DelayedDeliveryOptions { LockRetryInterval = TimeSpan.FromMilliseconds(milliseconds) };

        Assert.That(options.LockRetryInterval, Is.EqualTo(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(60_001)]
    public void LockRetryInterval_rejects_values_outside_the_allowed_range(int milliseconds)
    {
        var options = new DelayedDeliveryOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.LockRetryInterval = TimeSpan.FromMilliseconds(milliseconds));
    }
}
