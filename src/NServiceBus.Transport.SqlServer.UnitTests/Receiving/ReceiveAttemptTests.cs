namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;

public class ReceiveAttemptTests
{
    [Test]
    public async Task WhenAReceiveIsAttempted_AndNothingIsReceived_ThenSignalsTheLatchAndStopsTheBatch()
    {
        var noMessagesQueue = new FakeQueue(MessageReadResult.NoMessage);
        var latch = new ReceiveCountdownEvent(1);
        var stopBatch = new CancellationTokenSource();
        var batchActivity = new BatchActivity();
        _ = batchActivity.BeginNextBatch();
        var attempt = new ReceiveAttempt(noMessagesQueue, new ReceiveCursor(anchoringEnabled: true), batchActivity, latch.GetSignaler(), stopBatch);

        _ = await attempt.Receive(null, null, CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(latch.WaitAsync(CancellationToken).IsCompleted, Is.True, "latch signalled");
            Assert.That(stopBatch.IsCancellationRequested, Is.True, "empty receive stops the batch");
            Assert.That(batchActivity.BeginNextBatch(), Is.EqualTo(LastBatchResult.LastBatchWasEmpty), "nothing received");
        });
    }

    [Test]
    public async Task WhenAReceiveIsAttempted_AndAMessageIsReceived_ThenSignalsTheLatchAndAllowsTheBatchToContinue()
    {
        var latch = new ReceiveCountdownEvent(1);
        var stopBatch = new CancellationTokenSource();
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        var batchActivity = new BatchActivity();
        _ = batchActivity.BeginNextBatch();
        var message = MessageReadResult.Success(new Message("1", string.Empty, Array.Empty<byte>(), false), 1);
        var attempt = new ReceiveAttempt(new FakeQueue(message), cursor, batchActivity, latch.GetSignaler(), stopBatch);

        _ = await attempt.Receive(null, null, CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(latch.WaitAsync(CancellationToken).IsCompleted, Is.True, "latch signalled");
            Assert.That(stopBatch.IsCancellationRequested, Is.False, "batch continues");
            Assert.That(batchActivity.BeginNextBatch(), Is.EqualTo(LastBatchResult.PreviousBatchHadResultsOrNoPreviousBatch), "message received");
        });
    }

    [Test]
    public async Task WhenAReceiveHasAlreadyBeenAttempted_AndReceiveIsCalledAgain_ThenThrows()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        var attempt = new ReceiveAttempt(new FakeQueue(MessageReadResult.NoMessage), cursor, new BatchActivity(), new ReceiveCountdownEvent(1).GetSignaler(), new CancellationTokenSource());

        _ = await attempt.Receive(null, null, CancellationToken);

        Assert.ThrowsAsync<InvalidOperationException>(() => attempt.Receive(null, null, CancellationToken));
    }

    [Test]
    public async Task WhenAMessageIsReceived_AndTheAttemptIsCommitted_ThenTheAnchorAdvancesToTheReceivedRow()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        var attempt = CreateAttempt(cursor, MessageReadResult.Success(CreateMessage(), 10));

        _ = await attempt.Receive(null, null, CancellationToken);
        attempt.Settle(ProcessOutcome.Committed);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(10));
    }

    [Test]
    public async Task WhenAMessageIsReceived_AndTheAnchorAdvancesPastIt_AndTheAttemptIsRolledBack_ThenTheAnchorRetreatsToTheReceivedRow()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        var attempt = CreateAttempt(cursor, MessageReadResult.Success(CreateMessage(), 7));

        _ = await attempt.Receive(null, null, CancellationToken);
        cursor.AdvanceAnchor(10); // a concurrent receive committed a later row
        attempt.Settle(ProcessOutcome.RolledBack);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(6));
    }

    [Test]
    public void WhenTheReceiveQueryThrows_AndTheAttemptIsRolledBack_ThenTheAnchorIsUnchanged()
    {
        var cursor = new ReceiveCursor(anchoringEnabled: true);
        cursor.AdvanceAnchor(10);
        var attempt = new ReceiveAttempt(new ThrowingQueue(), cursor, new BatchActivity(), new ReceiveCountdownEvent(1).GetSignaler(), new CancellationTokenSource());

        Assert.ThrowsAsync<InvalidOperationException>(() => attempt.Receive(null, null, CancellationToken));
        attempt.Settle(ProcessOutcome.RolledBack);

        Assert.That(cursor.GetAnchor().Anchor, Is.EqualTo(10));
    }

    static ReceiveAttempt CreateAttempt(ReceiveCursor cursor, MessageReadResult result) =>
        new(new FakeQueue(result), cursor, new BatchActivity(), new ReceiveCountdownEvent(1).GetSignaler(), new CancellationTokenSource());

    static Message CreateMessage() => new("1", string.Empty, Array.Empty<byte>(), false);

    static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    class FakeQueue(MessageReadResult result) : TableBasedQueue(new SqlServerConstants(), "[dbo].[queue]", "queue", false)
    {
        public override Task<MessageReadResult> TryReceive(DbConnection connection, DbTransaction transaction, long anchor, CancellationToken cancellationToken = default)
            => Task.FromResult(result);

        protected override Task SendRawMessage(MessageRow message, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    class ThrowingQueue() : TableBasedQueue(new SqlServerConstants(), "[dbo].[queue]", "queue", false)
    {
        public override Task<MessageReadResult> TryReceive(DbConnection connection, DbTransaction transaction, long anchor, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Receive query failed");

        protected override Task SendRawMessage(MessageRow message, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
