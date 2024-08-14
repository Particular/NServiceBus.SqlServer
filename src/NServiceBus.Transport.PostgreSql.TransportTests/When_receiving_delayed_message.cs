namespace NServiceBus.TransportTests
{
    using System;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using NUnit.Framework;
    using Transport;

    public class When_receiving_delayed_message : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_expose_receiving_address(TransportTransactionMode transactionMode)
        {
            var onReceived = CreateTaskCompletionSource<MessageContext>();

            DateTimeOffset after = DateTimeOffset.MinValue;

            await Initialize(
                (context, _) =>
                {
                    onReceived.SetResult(context);
                    return Task.CompletedTask;
                },
                (_, __) => Task.FromResult(ErrorHandleResult.Handled),
                transactionMode);

            var before = DateTimeOffset.UtcNow;
            var delay = TimeSpan.FromSeconds(2);
            var dispatchProperties = new DispatchProperties { DelayDeliveryWith = new DelayDeliveryWith(delay) };

            //We send the message _before_ we start receiving to avoid a race condition between the first delayed message poll request
            //and the event that is triggered when the dispatcher stores a delayed message
            //that leads to setting the next poll time 1 minute into the future.
            await SendMessage(InputQueueName, dispatchProperties: dispatchProperties);

            await receiver.StartReceive();

            _ = await onReceived.Task;
            after = DateTimeOffset.UtcNow;

            Assert.That(after - before, Is.GreaterThan(delay));
        }
    }
}