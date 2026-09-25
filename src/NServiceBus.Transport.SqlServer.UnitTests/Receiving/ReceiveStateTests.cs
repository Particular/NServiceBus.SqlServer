namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;

public class ReceiveStateTests
{
    [Test]
    public void Starts_at_the_head_of_the_queue()
    {
        var state = new ReceiveState(anchoringEnabled: true);

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(0));
    }

    [Test]
    public void Advances_monotonically()
    {
        var state = new ReceiveState(anchoringEnabled: true);

        state.AdvanceAnchor(10);
        state.AdvanceAnchor(5); // out-of-order completion of a concurrent receive must not move the anchor back

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(10));
    }

    [Test]
    public void Stays_at_the_head_of_the_queue_when_anchoring_is_disabled()
    {
        var state = new ReceiveState(anchoringEnabled: false);

        state.ApplyPeekResult(100);
        state.AdvanceAnchor(200);
        state.RescanFrom(150);
        state.SweepFromHead();

        Assert.That(state.GetAnchor(), Is.EqualTo(ReceiveAnchor.Fast(0)));
    }

    [Test]
    public void Retreats_to_just_before_a_rolled_back_row()
    {
        var state = new ReceiveState(anchoringEnabled: true);

        state.AdvanceAnchor(10);
        state.RescanFrom(7);

        // anchored receives seek strictly past the anchor, so row 7 is visible again
        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(6));
    }

    [Test]
    public void Retreating_never_moves_the_anchor_forward()
    {
        var state = new ReceiveState(anchoringEnabled: true);

        state.AdvanceAnchor(5);
        state.RescanFrom(10); // the rolled back row is already past the anchor

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(5));
    }

    [Test]
    public void A_peek_past_the_anchor_moves_it_forward_past_the_churned_head()
    {
        var state = new ReceiveState(anchoringEnabled: true);

        state.ApplyPeekResult(20);

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(19));
    }

    [Test]
    public void A_peek_behind_the_anchor_starts_a_sweep_that_commits_do_not_skip()
    {
        var state = new ReceiveState(anchoringEnabled: true);
        state.AdvanceAnchor(10);

        state.ApplyPeekResult(4);
        state.AdvanceAnchor(20); // a concurrent receive commits a later row

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(3));
    }

    [Test]
    public void A_rollback_starts_a_sweep_that_commits_do_not_skip()
    {
        var state = new ReceiveState(anchoringEnabled: true);
        state.AdvanceAnchor(10);

        state.RescanFrom(7);
        state.AdvanceAnchor(20);

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(6));
    }

    [Test]
    public void A_rollback_lowers_a_running_sweep()
    {
        var state = new ReceiveState(anchoringEnabled: true);
        state.AdvanceAnchor(10);
        state.RescanFrom(7);

        state.RescanFrom(4);

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(3));
    }

    [Test]
    public void A_sweep_receive_that_finds_a_stranded_row_moves_the_sweep_past_it()
    {
        var state = new ReceiveState(anchoringEnabled: true);
        state.AdvanceAnchor(10);
        state.ApplyPeekResult(4);

        state.AdvanceOrEndSweep(state.GetAnchor(), 5);

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(5));
    }

    [Test]
    public void A_sweep_receive_that_reaches_the_anchor_ends_the_sweep()
    {
        var state = new ReceiveState(anchoringEnabled: true);
        state.AdvanceAnchor(10);
        state.ApplyPeekResult(4);
        state.AdvanceAnchor(20);

        state.AdvanceOrEndSweep(state.GetAnchor(), 21);

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(20));
    }

    [Test]
    public void An_empty_sweep_receive_ends_the_sweep()
    {
        var state = new ReceiveState(anchoringEnabled: true);
        state.AdvanceAnchor(10);
        state.RescanFrom(7);

        state.AdvanceOrEndSweep(state.GetAnchor(), null);

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(10));
    }

    [Test]
    public void A_receive_from_an_earlier_sweep_does_not_end_a_lowered_one()
    {
        var state = new ReceiveState(anchoringEnabled: true);
        state.AdvanceAnchor(10);
        state.RescanFrom(7);
        var staleSeek = state.GetAnchor();

        state.RescanFrom(3); // rolled back while the earlier receive's query ran
        state.AdvanceOrEndSweep(staleSeek, 11);

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(2));
    }

    [Test]
    public void A_receive_from_the_anchor_does_not_touch_the_sweep()
    {
        var state = new ReceiveState(anchoringEnabled: true);
        state.AdvanceAnchor(10);
        var anchoredSeek = state.GetAnchor();

        state.RescanFrom(7);
        state.AdvanceOrEndSweep(anchoredSeek, 11);

        Assert.That(state.GetAnchor().Anchor, Is.EqualTo(6));
    }

    [Test]
    public void A_head_sweep_seeks_from_the_start_of_the_queue()
    {
        var state = new ReceiveState(anchoringEnabled: true);
        state.AdvanceAnchor(10);

        state.SweepFromHead();
        state.AdvanceAnchor(20);

        Assert.That(state.GetAnchor(), Is.EqualTo(ReceiveAnchor.Sweep(0)));
    }

    [Test]
    public void A_head_sweep_is_not_needed_while_the_anchor_is_at_the_head()
    {
        var state = new ReceiveState(anchoringEnabled: true);

        state.SweepFromHead();

        Assert.That(state.GetAnchor(), Is.EqualTo(ReceiveAnchor.Fast(0)));
    }

    [Test]
    public void First_batch_does_not_back_off()
    {
        var state = new ReceiveState(anchoringEnabled: true);

        Assert.That(state.BeginBatch(), Is.True);
    }

    [Test]
    public void Reports_whether_the_previous_batch_received_anything()
    {
        var state = new ReceiveState(anchoringEnabled: true);
        _ = state.BeginBatch();

        Assert.That(state.BeginBatch(), Is.False, "nothing received in the previous batch");

        state.MarkReceived();

        Assert.Multiple(() =>
        {
            Assert.That(state.BeginBatch(), Is.True, "previous batch received a message");
            Assert.That(state.BeginBatch(), Is.False, "starting a batch clears the flag");
        });
    }
}
