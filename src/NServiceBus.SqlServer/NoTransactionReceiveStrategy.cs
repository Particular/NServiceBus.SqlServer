namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;

    class NoTransactionReceiveStrategy : ReceiveStrategy
    {
        //TODO: do we need to pass the messageId here. Cons: idle runs? Ordering?, pros: fewer locking?
        public override async Task RecieveMessage(string messageId, TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage)
        {
            var readResult = inputQueue.TryReceive(messageId);

            if (readResult.IsPoison)
            {
                errorQueue.Send(readResult.DataRecord);
                return;
            }

            if (readResult.Successful)
            {
                var message = readResult.Message;

                using (var bodyStream = message.BodyStream)
                {
                    bodyStream.Position = 0;
                    var pushContext = new PushContext(message.Id, message.Headers, bodyStream, new ContextBag());
                    await onMessage(pushContext).ConfigureAwait(false);
                }
            }
        }
    }
}