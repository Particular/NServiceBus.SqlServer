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

            await StartPump(
                (context, _) =>
                {
                    onReceived.SetResult(context);
                    return Task.CompletedTask;
                },
                (context, _) =>
                {
                    Assert.Fail("Unexpected exception");
                    return Task.FromResult(ErrorHandleResult.Handled);
                },
                transactionMode);

            var delay = TimeSpan.FromSeconds(5);
            var dispatchProperties = new DispatchProperties
            {
                DelayDeliveryWith = new DelayDeliveryWith(delay)
            };

            var before = DateTimeOffset.UtcNow;

            await SendMessage(InputQueueName, dispatchProperties: dispatchProperties);

            var _ = await onReceived.Task;

            var after = DateTimeOffset.UtcNow;

            Assert.True(after - before > delay);
        }
    }
}