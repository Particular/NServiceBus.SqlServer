namespace NServiceBus.Transport.SqlServer.UnitTests;

using NUnit.Framework;

public class DelayedDeliveryOptionsTests
{
    [Test]
    public void Batch_size_defaults_to_200() =>
        Assert.That(new DelayedDeliveryOptions().BatchSize, Is.EqualTo(200));

    [Test]
    public void Single_mover_uses_the_default_batch_size() =>
        Assert.That(new DelayedDeliveryOptions().GetBatchSize(useIndependentMovers: false), Is.EqualTo(200));

    [Test]
    public void Independent_movers_keep_a_batch_size_of_100_by_default() =>
        Assert.That(new DelayedDeliveryOptions().GetBatchSize(useIndependentMovers: true), Is.EqualTo(100));

    [TestCase(false)]
    [TestCase(true)]
    public void Configured_batch_size_wins(bool useIndependentMovers)
    {
        var options = new DelayedDeliveryOptions { BatchSize = 42 };

        Assert.That(options.GetBatchSize(useIndependentMovers), Is.EqualTo(42));
    }
}
