namespace NServiceBus.TransportTests
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using NUnit.Framework;
    using Transport;
    using Unicast.Messages;

    public class When_publishing_message : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_expose_receiving_address(TransportTransactionMode transactionMode)
        {
            var onReceived = CreateTaskCompletionSource<MessageContext>();

            DateTimeOffset after = DateTimeOffset.MinValue;

            await StartPump(
                (context, _) =>
                {
                    onReceived.SetResult(context);
                    return Task.CompletedTask;
                },
                (_, __) => Task.FromResult(ErrorHandleResult.Handled),
                transactionMode);

            await receiver.Subscriptions.SubscribeAll([new MessageMetadata(typeof(MyEvent))],
                new ContextBag());

            await PublishMessage(typeof(MyEvent));

            var ctx = await onReceived.Task;

            Assert.That(ctx, Is.Not.Null);
        }
    }

    public class MyEvent : IEvent
    {
    }
}
