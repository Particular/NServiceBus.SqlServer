namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Extensibility;
using Transport;
using Sql.Shared;
using Unicast.Messages;
using NUnit.Framework;

public class MessageReceiverStrandedRowTests
{
    [Test]
    public async Task WhenProcessingALongRunningBatch_AndARowReappearsBehindTheAnchor_ThatRowIsPickedUpWithinTheBatch()
    {
        const long strandedRowVersion = 5;
        const int receivesBeforePuttingStrandedRowIntoQueue = 300;
        // Run the sweep every 10 messages
        var headSweepInterval = TimeSpan.FromSeconds(1);
        var eachMessageReceivedProgressesTimeBy = TimeSpan.FromMilliseconds(100);

        var timeProvider = new FakeTimeProvider();
        var queue = new InMemoryQueue(rowVersions: Enumerable.Range(start: 100, count: 2_000));
        var peeker = new ScriptedPeeker(
            new PeekResult(messageCount: 2_000, lowestRowVersion: 100), // First peek misses stranded row
            PeekResult.Empty); // Force only one batch to be processed by returning empty on every subsequent peek call.

        var strandedReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        queue.OnReceive = (_, receiveNumber, rowVersion) =>
        {
            timeProvider.Advance(eachMessageReceivedProgressesTimeBy);

            if (receiveNumber == receivesBeforePuttingStrandedRowIntoQueue)
            {
                queue.Add(strandedRowVersion);
            }

            if (rowVersion == strandedRowVersion)
            {
                strandedReceived.TrySetResult();
            }
        };

        var receiver = CreateReceiver(queue, peeker, timeProvider, headSweepInterval);

        await receiver.Initialize(new PushRuntimeSettings(4), (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken);
        await receiver.StartReceive(CancellationToken);
        var completed = await Task.WhenAny(strandedReceived.Task, Task.Delay(TimeSpan.FromSeconds(10), CancellationToken));
        await receiver.StopReceive(CancellationToken);

        Assert.That(completed, Is.SameAs(strandedReceived.Task), "the head sweep should have reached the stranded row within the batch");
    }

    [Test]
    public async Task WhenProcessingAShortBatch_AndARowReappearsBehindTheAnchor_ThatRowIsPickedUpByTheNextPeek()
    {
        // The head sweep interval is never reached, so the stranded row can only be
        // found by the next batch's peek reporting it as the lowest row.
        const long strandedRowVersion = 5;
        const int receivesBeforeStranding = 10;
        var headSweepInterval = TimeSpan.FromDays(999); // The sweep never runs.

        var timeProvider = new FakeTimeProvider();
        var queue = new InMemoryQueue(rowVersions: Enumerable.Range(start: 20, count: 100));
        var peeker = new ScriptedPeeker(
            new PeekResult(messageCount: 100, lowestRowVersion: 20),
            new PeekResult(messageCount: 1, lowestRowVersion: strandedRowVersion),
            PeekResult.Empty);

        var strandedReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        queue.OnReceive = (_, receiveNumber, rowVersion) =>
        {
            if (receiveNumber == receivesBeforeStranding)
            {
                queue.Add(strandedRowVersion);
            }

            if (rowVersion == strandedRowVersion)
            {
                strandedReceived.TrySetResult();
            }
        };

        var receiver = CreateReceiver(queue, peeker, timeProvider, headSweepInterval: headSweepInterval);

        await receiver.Initialize(new PushRuntimeSettings(4), (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken);
        await receiver.StartReceive(CancellationToken);
        var completed = await Task.WhenAny(strandedReceived.Task, Task.Delay(TimeSpan.FromSeconds(10), CancellationToken));
        await receiver.StopReceive(CancellationToken);

        Assert.That(completed, Is.SameAs(strandedReceived.Task), "the next peek should have moved the anchor back to the stranded row");
    }

    [Test]
    public async Task WhenProcessingABatch_AndTheSweepIntervalHasNotPassed_ReceivesContinueFromTheAnchor()
    {
        var headSweepInterval = TimeSpan.FromDays(999); // The sweep never runs.
        var queue = new InMemoryQueue(rowVersions: Enumerable.Range(start: 100, count: 20));
        var peeker = new ScriptedPeeker(new PeekResult(messageCount: 20, lowestRowVersion: 100), PeekResult.Empty);

        var timeProvider = new FakeTimeProvider();
        var anchors = new List<long>();
        var allReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        queue.OnReceive = (anchor, receiveNumber, _) =>
        {
            lock (anchors)
            {
                anchors.Add(anchor);
            }

            if (receiveNumber == 20)
            {
                allReceived.TrySetResult();
            }
        };

        var receiver = CreateReceiver(queue, peeker, timeProvider, headSweepInterval: headSweepInterval);

        await receiver.Initialize(new PushRuntimeSettings(1), (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken);
        await receiver.StartReceive(CancellationToken);
        var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(10), CancellationToken));
        await receiver.StopReceive(CancellationToken);

        Assert.That(completed, Is.SameAs(allReceived.Task), "every row should have been received");
        Assert.That(anchors, Has.None.LessThan(99), "the head should not be swept before the interval passes");
    }

    [Test]
    public async Task WhenProcessingABatch_AndTheSweepIntervalPasses_TheHeadIsSweptOnce()
    {
        const int receiveWhenIntervalPasses = 3;
        var headSweepInterval = TimeSpan.FromSeconds(1);
        var queue = new InMemoryQueue(rowVersions: Enumerable.Range(start: 100, count: 20));
        var peeker = new ScriptedPeeker(new PeekResult(messageCount: 20, lowestRowVersion: 100), PeekResult.Empty);

        var anchors = new List<long>();
        var allReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeProvider = new FakeTimeProvider();

        queue.OnReceive = (anchor, receiveNumber, _) =>
        {
            if (receiveNumber == receiveWhenIntervalPasses)
            {
                timeProvider.Advance(headSweepInterval);
            }

            lock (anchors)
            {
                anchors.Add(anchor);
            }

            if (receiveNumber == 20)
            {
                allReceived.TrySetResult();
            }
        };

        var receiver = CreateReceiver(queue, peeker, timeProvider, headSweepInterval: headSweepInterval);

        await receiver.Initialize(new PushRuntimeSettings(1), (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken);
        await receiver.StartReceive(CancellationToken);
        var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(10), CancellationToken));
        await receiver.StopReceive(CancellationToken);

        Assert.That(completed, Is.SameAs(allReceived.Task), "every row should have been received");
        Assert.That(anchors.Count(anchor => anchor == HeadOfQueue), Is.EqualTo(1), "the head should be swept once per interval");
    }

    const long HeadOfQueue = 0;

    static MessageReceiver CreateReceiver(InMemoryQueue queue, IPeekMessagesInQueue peeker, TimeProvider timeProvider, TimeSpan? headSweepInterval = null)
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
            peeker,
            true,
            headSweepInterval ?? TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            new FakeSubscriptionManager(),
            false,
            classifier,
            timeProvider);
    }

    [SetUp]
    public void SetUp() => cancellationTokenSource = new CancellationTokenSource();

    [TearDown]
    public void TearDown()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    CancellationToken CancellationToken => cancellationTokenSource.Token;

    CancellationTokenSource cancellationTokenSource;

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

        public override Task<MessageReadResult> TryReceive(DbConnection connection, DbTransaction transaction, long seekAfter, CancellationToken cancellationToken = default)
        {
            long? received = null;
            int receiveNumber;

            lock (rows)
            {
                var past = rows.GetViewBetween(seekAfter + 1, long.MaxValue);
                if (past.Count > 0)
                {
                    received = past.Min;
                    _ = rows.Remove(past.Min);
                }

                receiveNumber = ++receives;
            }

            OnReceive?.Invoke(seekAfter, receiveNumber, received);

            return Task.FromResult(received is { } rowVersion
                ? MessageReadResult.Success(new Message(rowVersion.ToString(), string.Empty, Array.Empty<byte>(), false), rowVersion)
                : MessageReadResult.NoMessage);
        }

        protected override Task SendRawMessage(MessageRow message, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        readonly SortedSet<long> rows = [.. rowVersions];
        int receives;

        public InMemoryQueue(IEnumerable<int> rowVersions) : this(rowVersions.Select(i => (long)i)) { }
    }

    /// <summary>
    /// Returns the given peek results in order, then keeps returning the last one.
    /// </summary>
    class ScriptedPeeker(params PeekResult[] results) : IPeekMessagesInQueue
    {
        public Task<PeekResult> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default)
            => Task.FromResult(results[Math.Min(peeks++, results.Length - 1)]);

        public Task WaitForPeekDelay(CancellationToken cancellationToken = default) => Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);

        int peeks;
    }

    class CommittingStrategy(IExceptionClassifier exceptionClassifier) : ProcessStrategy(null, exceptionClassifier, null)
    {
        public override async Task<ProcessOutcome> ProcessMessage(ReceiveAttempt receiveAttempt, CancellationToken cancellationToken = default)
        {
            var receiveResult = await receiveAttempt.Receive(null, null, cancellationToken);

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
