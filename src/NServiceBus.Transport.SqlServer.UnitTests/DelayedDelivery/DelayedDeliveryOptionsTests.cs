namespace NServiceBus.Transport.SqlServer.UnitTests.DelayedDelivery;

using System;
using NUnit.Framework;

public class DelayedDeliveryOptionsTests
{
    [Test]
    public void DelayedMessageMoveLockDelay_defaults_to_null()
    {
        var options = new DelayedDeliveryOptions();

        Assert.That(options.DelayedMessageMoveLockDelay, Is.Null);
    }

    [Test]
    public void DelayedMessageMoveLockDelay_accepts_positive_value()
    {
        var options = new DelayedDeliveryOptions { DelayedMessageMoveLockDelay = TimeSpan.FromMilliseconds(900) };

        Assert.That(options.DelayedMessageMoveLockDelay, Is.EqualTo(TimeSpan.FromMilliseconds(900)));
    }

    [Test]
    public void DelayedMessageMoveLockDelay_can_be_reset_to_null()
    {
        var options = new DelayedDeliveryOptions { DelayedMessageMoveLockDelay = TimeSpan.FromSeconds(1) };

        options.DelayedMessageMoveLockDelay = null;

        Assert.That(options.DelayedMessageMoveLockDelay, Is.Null);
    }

    [Test]
    public void DelayedMessageMoveLockDelay_rejects_zero()
    {
        var options = new DelayedDeliveryOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.DelayedMessageMoveLockDelay = TimeSpan.Zero);
    }

    [Test]
    public void DelayedMessageMoveLockDelay_rejects_negative()
    {
        var options = new DelayedDeliveryOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.DelayedMessageMoveLockDelay = TimeSpan.FromMilliseconds(-1));
    }

    [Test]
    public void DelayedMessageMoveLockDelay_rejects_values_exceeding_int_max_milliseconds()
    {
        var options = new DelayedDeliveryOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.DelayedMessageMoveLockDelay = TimeSpan.FromMilliseconds(int.MaxValue) + TimeSpan.FromMilliseconds(1));
    }
}
