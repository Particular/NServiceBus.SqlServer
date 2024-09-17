namespace NServiceBus.TransportTests
{
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transport;

    public class When_delayed_delivery_disabled : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_work(TransportTransactionMode transactionMode)
        {
            CustomizeTransportDefinition = definition =>
            {
                ((PostgreSqlTransport)definition).DisableDelayedDelivery = true;
            };

            var onReceived = CreateTaskCompletionSource<MessageContext>();

            await StartPump(
                (context, _) =>
                {
                    onReceived.SetResult(context);
                    return Task.CompletedTask;
                },
                (_, __) => Task.FromResult(ErrorHandleResult.Handled),
                transactionMode);

            await SendMessage(InputQueueName);

            var ctx = await onReceived.Task;

            Assert.That(ctx, Is.Not.Null);
        }
    }
}