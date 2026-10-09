namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using NServiceBus.Extensibility;
using NServiceBus.Transport;
using NServiceBus.Transport.Sql.Shared;
using NServiceBus.Unicast.Messages;
using NUnit.Framework;

public class MessageReceiverStrandedRowTests
{
    [Test]
    public async Task Picks_up_a_row_that_reappears_behind_the_anchor_by_the_next_head_sweep()
    {
        // Every receive moves the clock on 10ms, so a head sweep (1s minimum interval) runs every ~100
        // receives. After 300 receives a row reappears far behind the anchor, as if it rolled back
        // on another instance; concurrent commits keep advancing the anchor past it.
        const long strandedRowVersion = 5;
        const int receivesBeforeStranding = 300;

        var timeProvider = new FakeTimeProvider();
        var queue = new InMemoryQueue(Enumerable.Range(100, 10_000).Select(i => (long)i));
        var strandedReceivedAt = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        queue.OnReceive = (_, receiveNumber, rowVersion) =>
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(10));

            if (receiveNumber == receivesBeforeStranding)
            {
                queue.Add(strandedRowVersion);
            }

            if (rowVersion == strandedRowVersion)
            {
                strandedReceivedAt.TrySetResult(receiveNumber);
            }
        };

        var receiver = CreateReceiver(queue, timeProvider);

        await receiver.Initialize(new PushRuntimeSettings(4), (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken).ConfigureAwait(false);
        await receiver.StartReceive(CancellationToken).ConfigureAwait(false);
        var receivedAt = await strandedReceivedAt.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken).ConfigureAwait(false);
        await receiver.StopReceive(CancellationToken).ConfigureAwait(false);

        // at most one sweep interval, then the head sweep finds it first
        Assert.That(receivedAt - receivesBeforeStranding, Is.LessThanOrEqualTo(110));
    }

    [Test]
    public async Task Sweeps_from_the_head_once_per_interval_on_a_busy_queue()
    {
        // every receive finds a row, so the batch never ends; each one moves the clock on 300ms, so
        // the 1s minimum sweep interval has passed by the fifth
        var timeProvider = new FakeTimeProvider();
        var queue = new InMemoryQueue(Enumerable.Range(100, 10_000).Select(i => (long)i));
        var anchors = new List<long>();
        var sixReceives = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        queue.OnReceive = (anchor, receiveNumber, _) =>
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(300));

            lock (anchors)
            {
                anchors.Add(anchor);
            }

            if (receiveNumber == 6)
            {
                sixReceives.TrySetResult();
            }
        };

        var receiver = CreateReceiver(queue, timeProvider);

        await receiver.Initialize(new PushRuntimeSettings(1), (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken).ConfigureAwait(false);
        await receiver.StartReceive(CancellationToken).ConfigureAwait(false);
        await sixReceives.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken).ConfigureAwait(false);
        await receiver.StopReceive(CancellationToken).ConfigureAwait(false);

        // from the peek's lowest row, then the head sweep, which finds nothing stranded and hands back to the anchor
        Assert.That(anchors.Take(6), Is.EqualTo(new long[] { 99, 100, 101, 102, 0, 104 }));
    }

    static MessageReceiver CreateReceiver(InMemoryQueue queue, TimeProvider timeProvider)
    {
        var classifier = new SqlServerExceptionClassifier();

        return new MessageReceiver(
            new SqlServerTransport("Server=unused;Trusted_Connection=True"),
            "receiver",
            "queue",
            "error",
            (_, _, _) => { },
            _ => new CommittingStrategy(classifier),
            _ => queue,
            new FakePurger(),
            new InMemoryPeeker(),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            new FakeSubscriptionManager(),
            false,
            classifier,
            timeProvider);
    }

    static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    /// <summary>
    /// Receives the lowest row strictly past the anchor, like the anchored receive query.
    /// </summary>
    class InMemoryQueue(IEnumerable<long> rowVersions) : TableBasedQueue(new SqlServerConstants(), "[dbo].[queue]", "queue", false)
    {
        public Action<long, int, long?> OnReceive { get; set; }

        public void Add(long rowVersion)
        {
            lock (rows)
            {
                _ = rows.Add(rowVersion);
            }
        }

        public PeekResult Peek()
        {
            lock (rows)
            {
                return rows.Count == 0 ? PeekResult.Empty : new PeekResult(rows.Count, rows.Min);
            }
        }

        public override Task<MessageReadResult> TryReceive(DbConnection connection, DbTransaction transaction, long anchor, CancellationToken cancellationToken = default)
        {
            long? received = null;
            int receiveNumber;

            lock (rows)
            {
                var past = rows.GetViewBetween(anchor + 1, long.MaxValue);
                if (past.Count > 0)
                {
                    received = past.Min;
                    _ = rows.Remove(past.Min);
                }

                receiveNumber = ++receives;
            }

            OnReceive?.Invoke(anchor, receiveNumber, received);

            return Task.FromResult(received is { } rowVersion
                ? MessageReadResult.Success(new Message(rowVersion.ToString(), string.Empty, Array.Empty<byte>(), false), rowVersion)
                : MessageReadResult.NoMessage);
        }

        protected override Task SendRawMessage(MessageRow message, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        readonly SortedSet<long> rows = [.. rowVersions];
        int receives;
    }

    class InMemoryPeeker : IPeekMessagesInQueue
    {
        public TimeSpan PeekDelay => TimeSpan.FromMilliseconds(200);

        public Task<PeekResult> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default)
            => Task.FromResult(((InMemoryQueue)inputQueue).Peek());

        public Task WaitForPeekDelay(CancellationToken cancellationToken = default) => Task.Delay(PeekDelay, cancellationToken);
    }

    class CommittingStrategy(IExceptionClassifier exceptionClassifier) : ProcessStrategy(null, exceptionClassifier, null)
    {
        public override async Task<ProcessOutcome> ProcessMessage(ReceiveAttempt receiveAttempt, CancellationToken cancellationToken = default)
        {
            var receiveResult = await receiveAttempt.Receive(null, null, cancellationToken).ConfigureAwait(false);

            return receiveResult == MessageReadResult.NoMessage
                ? ProcessOutcome.NoMessage
                : ProcessOutcome.Committed;
        }
    }

    class FakePurger : IPurgeQueues
    {
        public Task<int> Purge(TableBasedQueue queue, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    class FakeSubscriptionManager : ISubscriptionManager
    {
        public Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
