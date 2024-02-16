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

            await StartPump(
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

            await SendMessage(InputQueueName, dispatchProperties: dispatchProperties);

            _ = await onReceived.Task;
            after = DateTimeOffset.UtcNow;

            Assert.True(after - before > delay);
        }
    }
}