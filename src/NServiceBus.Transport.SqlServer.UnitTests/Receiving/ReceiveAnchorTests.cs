namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using System;
using Microsoft.Extensions.Time.Testing;
using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;

public class ReceiveAnchorTests
{
    [Test]
    public void Starts_at_the_head_of_the_queue()
    {
        var anchor = new ReceiveAnchor(TimeSpan.FromSeconds(1), new FakeTimeProvider());

        Assert.That(anchor.GetCurrent(), Is.EqualTo(0));
    }

    [Test]
    public void Advances_monotonically()
    {
        var anchor = new ReceiveAnchor(TimeSpan.FromSeconds(1), new FakeTimeProvider());

        anchor.Advance(10);
        anchor.Advance(5); // out-of-order completion of a concurrent receive must not move the anchor back

        Assert.That(anchor.GetCurrent(), Is.EqualTo(10));
    }

    [Test]
    public void Reset_moves_back_to_the_head()
    {
        var anchor = new ReceiveAnchor(TimeSpan.FromSeconds(1), new FakeTimeProvider());

        anchor.Advance(10);
        anchor.Reset();

        Assert.That(anchor.GetCurrent(), Is.EqualTo(0));
    }

    [Test]
    public void Periodically_forces_a_scan_from_the_head()
    {
        var timeProvider = new FakeTimeProvider();
        var anchor = new ReceiveAnchor(TimeSpan.FromSeconds(1), timeProvider);

        anchor.Advance(10);
        Assert.That(anchor.GetCurrent(), Is.EqualTo(10));

        timeProvider.Advance(TimeSpan.FromSeconds(1.5));

        Assert.Multiple(() =>
        {
            // one head rescan is due, subsequent receives resume from the anchor
            Assert.That(anchor.GetCurrent(), Is.EqualTo(0));
            Assert.That(anchor.GetCurrent(), Is.EqualTo(10));
        });
    }

    [Test]
    public void Advancing_does_not_postpone_the_head_rescan()
    {
        var timeProvider = new FakeTimeProvider();
        var anchor = new ReceiveAnchor(TimeSpan.FromSeconds(1), timeProvider);

        for (var i = 1; i <= 4; i++)
        {
            anchor.Advance(i);
            timeProvider.Advance(TimeSpan.FromSeconds(0.4));
        }

        // 1.6s elapsed with continuous receives: a head rescan must still have become due
        Assert.That(anchor.GetCurrent(), Is.EqualTo(0));
    }
}
