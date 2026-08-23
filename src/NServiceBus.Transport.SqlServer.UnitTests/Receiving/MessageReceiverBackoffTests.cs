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
        var peeker = new CountingPeeker();
        var receiver = CreateReceiver(peeker, emptyBatchBackoff: TimeSpan.FromMilliseconds(200));

        await receiver.Initialize(new PushRuntimeSettings(8), (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled)).ConfigureAwait(false);
        await receiver.StartReceive().ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(700)).ConfigureAwait(false);
        await receiver.StopReceive().ConfigureAwait(false);

        // ~700ms with a 200ms backoff allows for a handful of peek iterations; an unthrottled
        // pump reaches hundreds.
        Assert.That(peeker.PeekCount, Is.LessThanOrEqualTo(6));
    }

    static MessageReceiver CreateReceiver(CountingPeeker peeker, TimeSpan emptyBatchBackoff)
    {
        var queue = new FakeQueue();
        var classifier = new SqlServerExceptionClassifier();

        return new MessageReceiver(
            new SqlServerTransport("Server=unused;Trusted_Connection=True"),
            "receiver",
            "queue",
            "error",
            (_, _, _) => { },
            _ => new EmptyReceiveStrategy(classifier),
            _ => queue,
            new FakePurger(),
            peeker,
            TimeSpan.FromSeconds(30),
            emptyBatchBackoff,
            new FakeSubscriptionManager(),
            false,
            classifier);
    }

    class CountingPeeker : IPeekMessagesInQueue
    {
        int peekCount;

        public int PeekCount => Volatile.Read(ref peekCount);

        public Task<int> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref peekCount);
            return Task.FromResult(10);
        }
    }

    class EmptyReceiveStrategy : ProcessStrategy
    {
        public EmptyReceiveStrategy(IExceptionClassifier exceptionClassifier)
            : base(null, exceptionClassifier, null)
        {
        }

        public override Task ProcessMessage(CancellationTokenSource stopBatchCancellationTokenSource, ReceiveCountdownEvent.Signaler receiveCountdownEventSignaler, CancellationToken cancellationToken = default)
        {
            stopBatchCancellationTokenSource.Cancel();
            receiveCountdownEventSignaler.Signal();
            return Task.CompletedTask;
        }
    }

    class FakeQueue : TableBasedQueue
    {
        public FakeQueue() : base(new SqlServerConstants(), "[dbo].[queue]", "queue", false)
        {
        }

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
