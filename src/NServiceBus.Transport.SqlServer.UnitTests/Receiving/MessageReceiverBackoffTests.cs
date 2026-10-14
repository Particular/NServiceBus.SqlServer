namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using System;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Transport;
using Sql.Shared;
using Unicast.Messages;
using NUnit.Framework;

public class MessageReceiverBackoffTests
{
    [Test]
    public async Task WhenPeekReportsMessagesButReceivesComeUpEmpty_ThenWaitAPeekDelayBeforePeekingAgain()
    {
        var peekerReportingBacklog = new CountingPeeker(
            peekingReportesMessageCountOf: 10,
            signalAfterPeeks: 10);
        var queueWhereOtherNodesWinEveryReceive = new FakeQueue(() => MessageReadResult.NoMessage);
        var receiver = CreateReceiver(peekerReportingBacklog, queueWhereOtherNodesWinEveryReceive);

        await receiver.Initialize(new PushRuntimeSettings(8), (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled));
        await receiver.StartReceive();
        await peekerReportingBacklog.PeeksSignaled.WaitAsync(TimeSpan.FromSeconds(5));
        await receiver.StopReceive();

        Assert.That(peekerReportingBacklog.PeekDelayCount, Is.GreaterThanOrEqualTo(10 - 1 /* account for race between signal and peek */));
    }

    [Test]
    public async Task WhenPeekReportsMessagesAndBatchReceivesMessages_ThenDoesNotWaitOnPeekDelayBeforePeekingAgain()
    {
        // each batch receives one message and then comes up empty, ending the batch early
        var messageWaitingAfterPeek = 0;
        var queue = new FakeQueue(() => Interlocked.Exchange(ref messageWaitingAfterPeek, 0) == 1
            ? MessageReadResult.Success(new Message("1", string.Empty, Array.Empty<byte>(), false), 0)
            : MessageReadResult.NoMessage);
        var peeker = new CountingPeeker(peekingReportesMessageCountOf: 10, signalAfterPeeks: 10, onPeek: () => Interlocked.Exchange(ref messageWaitingAfterPeek, 1));
        var receiver = CreateReceiver(peeker, queue);

        await receiver.Initialize(new PushRuntimeSettings(1), (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled));
        await receiver.StartReceive();
        await peeker.PeeksSignaled.WaitAsync(TimeSpan.FromSeconds(5));
        await receiver.StopReceive();

        Assert.That(peeker.PeekDelayCount, Is.EqualTo(0));
    }

    static MessageReceiver CreateReceiver(CountingPeeker peeker, FakeQueue queue)
    {
        var classifier = new SqlServerExceptionClassifier();

        return new MessageReceiver(
            new SqlServerTransport("Server=unused;Trusted_Connection=True"),
            "receiver",
            "queue",
            "error",
            (_, _, _) => { },
            _ => new ReceivingStrategy(classifier),
            _ => queue,
            new FakePurger(),
            peeker,
            false,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30),
            new FakeSubscriptionManager(),
            false,
            classifier,
            TimeProvider.System);
    }

    class CountingPeeker(int peekingReportesMessageCountOf, int signalAfterPeeks, Action onPeek = null) : IPeekMessagesInQueue
    {
        readonly TaskCompletionSource peeksSignaled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int peekCount;
        int peekDelayCount;

        public int PeekCount => Volatile.Read(ref peekCount);
        public int PeekDelayCount => Volatile.Read(ref peekDelayCount);
        public Task PeeksSignaled => peeksSignaled.Task;

        public Task<PeekResult> Peek(TableBasedQueue inputQueue, long seekAfter, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref peekCount) >= signalAfterPeeks)
            {
                peeksSignaled.TrySetResult();
            }
            onPeek?.Invoke();
            return Task.FromResult(new PeekResult(peekingReportesMessageCountOf, 1));
        }

        public async Task WaitForPeekDelay(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref peekDelayCount);
            await Task.Yield();
        }
    }

    class ReceivingStrategy(IExceptionClassifier exceptionClassifier) : ProcessStrategy(null, exceptionClassifier, null)
    {
        public override async Task<ProcessOutcome> ProcessMessage(ReceiveAttempt receiveAttempt, CancellationToken cancellationToken = default)
        {
            var receiveResult = await receiveAttempt.Receive(null, null, cancellationToken);

            return receiveResult == MessageReadResult.NoMessage
                ? ProcessOutcome.NoMessage
                : ProcessOutcome.Committed;
        }
    }

    class FakeQueue(Func<MessageReadResult> receive) : TableBasedQueue(new SqlServerConstants(), "[dbo].[queue]", "queue", false)
    {
        public override Task<MessageReadResult> TryReceive(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, long anchor, CancellationToken cancellationToken = default)
            => Task.FromResult(receive());

        protected override Task SendRawMessage(MessageRow message, System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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
