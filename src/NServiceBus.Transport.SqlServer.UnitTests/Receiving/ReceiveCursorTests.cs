namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;

public class ReceiveCursorTests
{
    [Test]
    public void Starts_at_the_head_of_the_queue()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(0));
    }

    [Test]
    public void Advances_monotonically()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);

        cursor.AdvanceAnchor(10);
        cursor.AdvanceAnchor(5); // out-of-order completion of a concurrent receive must not move the anchor back

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(10));
    }

    [Test]
    public void Stays_at_the_head_of_the_queue_when_anchoring_is_disabled()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: false);

        cursor.ApplyPeekResult(100);
        cursor.AdvanceAnchor(200);
        cursor.RescanFrom(150);
        cursor.SweepFromHead();

        Assert.That(cursor.GetAnchor(), Is.EqualTo(ReceiveAnchor.HighWater(0)));
    }

    [Test]
    public void Retreats_to_just_before_a_rolled_back_row()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);

        cursor.AdvanceAnchor(10);
        cursor.RescanFrom(7);

        // anchored receives seek strictly past the anchor, so row 7 is visible again
        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(6));
    }

    [Test]
    public void Retreating_never_moves_the_anchor_forward()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);

        cursor.AdvanceAnchor(5);
        cursor.RescanFrom(10); // the rolled back row is already past the anchor

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(5));
    }

    [Test]
    public void A_peek_past_the_anchor_moves_it_forward_past_the_churned_head()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);

        cursor.ApplyPeekResult(20);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(19));
    }

    [Test]
    public void A_peek_behind_the_anchor_starts_a_sweep_that_commits_do_not_skip()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);

        cursor.ApplyPeekResult(4);
        cursor.AdvanceAnchor(20); // a concurrent receive commits a later row

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(3));
    }

    [Test]
    public void A_rollback_starts_a_sweep_that_commits_do_not_skip()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);

        cursor.RescanFrom(7);
        cursor.AdvanceAnchor(20);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(6));
    }

    [Test]
    public void A_rollback_lowers_a_running_sweep()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        cursor.RescanFrom(7);

        cursor.RescanFrom(4);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(3));
    }

    [Test]
    public void A_sweep_receive_that_finds_a_stranded_row_moves_the_sweep_past_it()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        cursor.ApplyPeekResult(4);

        cursor.AdvanceOrEndSweep(cursor.GetAnchor(), 5);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(5));
    }

    [Test]
    public void A_sweep_receive_that_reaches_the_anchor_ends_the_sweep()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        cursor.ApplyPeekResult(4);
        cursor.AdvanceAnchor(20);

        cursor.AdvanceOrEndSweep(cursor.GetAnchor(), 21);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(20));
    }

    [Test]
    public void An_empty_sweep_receive_ends_the_sweep()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        cursor.RescanFrom(7);

        cursor.AdvanceOrEndSweep(cursor.GetAnchor(), null);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(10));
    }

    [Test]
    public void A_receive_from_an_earlier_sweep_does_not_end_a_lowered_one()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        cursor.RescanFrom(7);
        var staleSeek = cursor.GetAnchor();

        cursor.RescanFrom(3); // rolled back while the earlier receive's query ran
        cursor.AdvanceOrEndSweep(staleSeek, 11);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(2));
    }

    [Test]
    public void A_receive_from_the_anchor_does_not_touch_the_sweep()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        var anchoredSeek = cursor.GetAnchor();

        cursor.RescanFrom(7);
        cursor.AdvanceOrEndSweep(anchoredSeek, 11);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(6));
    }

    [Test]
    public void A_head_sweep_seeks_from_the_start_of_the_queue()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);

        cursor.SweepFromHead();
        cursor.AdvanceAnchor(20);

        Assert.That(cursor.GetAnchor(), Is.EqualTo(ReceiveAnchor.Sweep(0)));
    }

    [Test]
    public void A_head_sweep_is_not_needed_while_the_anchor_is_at_the_head()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);

        cursor.SweepFromHead();

        Assert.That(cursor.GetAnchor(), Is.EqualTo(ReceiveAnchor.HighWater(0)));
    }
}
