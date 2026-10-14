namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using Sql.Shared;
using NUnit.Framework;

public class ReceiveCursorTests
{
    [Test]
    public void WhenCreated_ThenTheAnchorIsAtTheHeadOfTheQueue()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(0));
    }

    [Test]
    public void WhenTheAnchorAdvances_AndALowerRowIsCommittedLater_ThenTheAnchorDoesNotMoveBack()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);

        cursor.AdvanceAnchor(5); // out-of-order completion of a concurrent receive must not move the anchor back

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(10));
    }

    [Test]
    public void WhenAnchoringIsDisabled_AndTheCursorIsMoved_ThenTheAnchorStaysAtTheHeadOfTheQueue()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: false);

        cursor.ApplyPeekResult(100);
        cursor.AdvanceAnchor(200);
        cursor.RescanFrom(150);

        Assert.That(cursor.GetAnchor(), Is.EqualTo(ReceiveAnchor.HighWater(0)));
    }

    [Test]
    public void WhenTheAnchorHasAdvanced_AndAnEarlierRowIsRolledBack_ThenTheAnchorIsSetSoItWouldRefetchThatRow()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);

        cursor.RescanFrom(7);

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(6));
    }

    [Test]
    public void WhenARowPastTheAnchorIsRolledBack_ThenTheAnchorDoesNotMoveForward()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(5);

        cursor.RescanFrom(10); // the rolled back row is already past the anchor

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(5));
    }

    [Test]
    public void WhenAPeekFindsARowPastTheAnchor_ThenTheAnchorMovesForwardPastTheChurnedHead()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);

        cursor.ApplyPeekResult(20);

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(19));
    }

    [Test]
    public void WhenAPeekFindsARowBehindTheAnchor_AndALaterRowIsCommitted_ThenTheSweepIsNotSkipped()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);

        cursor.ApplyPeekResult(4);
        cursor.AdvanceAnchor(20); // a concurrent receive commits a later row

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(3));
    }

    [Test]
    public void WhenARowIsRolledBack_AndALaterRowIsCommitted_ThenTheSweepIsNotSkipped()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);

        cursor.RescanFrom(7);
        cursor.AdvanceAnchor(20);

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(6));
    }

    [Test]
    public void WhenASweepIsRunning_AndAnEarlierRowIsRolledBack_ThenTheSweepIsLowered()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        cursor.RescanFrom(7);

        cursor.RescanFrom(4);

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(3));
    }

    [Test]
    public void WhenASweepIsRunning_AndAReceiveFindsAStrandedRow_ThenTheSweepMovesPastIt()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        cursor.ApplyPeekResult(4);

        cursor.AdvanceOrEndSweep(cursor.GetAnchor(), 5);

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(5));
    }

    [Test]
    public void WhenASweepIsRunning_AndAReceiveReachesTheAnchor_ThenTheSweepEnds()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        cursor.ApplyPeekResult(4);
        cursor.AdvanceAnchor(20);

        cursor.AdvanceOrEndSweep(cursor.GetAnchor(), 21);

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(20));
    }

    [Test]
    public void WhenASweepIsRunning_AndAReceiveFindsNothing_ThenTheSweepEnds()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        cursor.RescanFrom(7);

        cursor.AdvanceOrEndSweep(cursor.GetAnchor(), null);

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(10));
    }

    [Test]
    public void WhenASweepIsLowered_AndAReceiveFromTheEarlierSweepCompletes_ThenTheLoweredSweepContinues()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        cursor.RescanFrom(7);
        var staleSeek = cursor.GetAnchor();

        cursor.RescanFrom(3); // rolled back while the earlier receive's query ran
        cursor.AdvanceOrEndSweep(staleSeek, 11);

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(2));
    }

    [Test]
    public void WhenASweepStarts_AndAReceiveFromTheAnchorCompletes_ThenTheSweepIsUnchanged()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        var anchoredSeek = cursor.GetAnchor();

        cursor.RescanFrom(7);
        cursor.AdvanceOrEndSweep(anchoredSeek, 11);

        Assert.That(cursor.GetAnchor().SeekAfter, Is.EqualTo(6));
    }

    [Test]
    public void WhenSweepingFromTheHead_AndALaterRowIsCommitted_ThenTheSweepSeeksFromTheStartOfTheQueue()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);

        cursor.SweepFromHead();
        cursor.AdvanceAnchor(20);

        Assert.That(cursor.GetAnchor(), Is.EqualTo(ReceiveAnchor.Sweep(0)));
    }

    [Test]
    public void WhenTheAnchorIsAtTheHead_AndSweepingFromTheHead_ThenNoSweepStarts()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);

        cursor.SweepFromHead();

        Assert.That(cursor.GetAnchor(), Is.EqualTo(ReceiveAnchor.HighWater(0)));
    }
}
