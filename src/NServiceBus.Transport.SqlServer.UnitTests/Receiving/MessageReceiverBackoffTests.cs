namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Transport;
using NServiceBus.Transport.Sql.Shared;
using NServiceBus.Unicast.Messages;
using NUnit.Framework;

public class MessageReceiverBackoffTests
{
    [Test]
    public async Task Backs_off_peeking_when_receives_come_up_empty()
    {
        // Simulates a node in a multi-node (competing consumer) setup that keeps losing the race
        // for messages: peek reports a backlog, but every receive comes up empty because other
        // nodes grabbed the messages. Without a backoff the pump re-peeks in a hot loop, which
        // multiplies the load on the queue table by the number of nodes.
        var peeker = new CountingPeeker(peekDelay: TimeSpan.FromMilliseconds(200));
        var receiver = CreateReceiver(peeker);

        await receiver.Initialize(new PushRuntimeSettings(8), (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled)).ConfigureAwait(false);
        await receiver.StartReceive().ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(700)).ConfigureAwait(false);
        await receiver.StopReceive().ConfigureAwait(false);

        // ~700ms with a 200ms backoff allows for a handful of peek iterations; an unthrottled
        // pump reaches hundreds.
        Assert.That(peeker.PeekCount, Is.LessThanOrEqualTo(6));
    }

    [Test]
    public async Task Does_not_back_off_when_the_batch_received_messages()
    {
        // each batch receives one message and then comes up empty, ending the batch early
        var messageAvailable = 0;
        var queue = new FakeQueue(() => Interlocked.Exchange(ref messageAvailable, 0) == 1
            ? MessageReadResult.Success(new Message("1", string.Empty, Array.Empty<byte>(), false), 0)
            : MessageReadResult.NoMessage);
        var peeker = new CountingPeeker(peekDelay: TimeSpan.FromMilliseconds(200), onPeek: () => Interlocked.Exchange(ref messageAvailable, 1));
        var receiver = CreateReceiver(peeker, queue);

        await receiver.Initialize(new PushRuntimeSettings(1), (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled)).ConfigureAwait(false);
        await receiver.StartReceive().ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(700)).ConfigureAwait(false);
        await receiver.StopReceive().ConfigureAwait(false);

        Assert.That(peeker.PeekCount, Is.GreaterThan(20));
    }

    static MessageReceiver CreateReceiver(CountingPeeker peeker) =>
        CreateReceiver(peeker, new FakeQueue(() => MessageReadResult.NoMessage));

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
            null,
            TimeSpan.FromSeconds(30),
            new FakeSubscriptionManager(),
            false,
            classifier,
            TimeProvider.System);
    }

    class CountingPeeker(TimeSpan peekDelay, Action onPeek = null) : IPeekMessagesInQueue
    {
        int peekCount;

        public TimeSpan PeekDelay => peekDelay;

        public int PeekCount => Volatile.Read(ref peekCount);

        public Task<PeekResult> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref peekCount);
            onPeek?.Invoke();
            return Task.FromResult(new PeekResult(10, 1));
        }

        public Task WaitForPeekDelay(CancellationToken cancellationToken = default) => Task.Delay(peekDelay, cancellationToken);
    }

    class ReceivingStrategy(IExceptionClassifier exceptionClassifier) : ProcessStrategy(null, exceptionClassifier, null)
    {
        public override async Task<ProcessOutcome> ProcessMessage(ReceiveAttempt receiveAttempt, CancellationToken cancellationToken = default)
        {
            var receiveResult = await receiveAttempt.Receive(null, null, cancellationToken).ConfigureAwait(false);

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
