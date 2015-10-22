namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;

    class NoTransactionReceiveStrategy
    {
        readonly TableBasedQueue inputQueue;
        readonly Func<PushContext, Task> onMessage;

        public NoTransactionReceiveStrategy(TableBasedQueue inputQueue, Func<PushContext, Task> onMessage)
        {
            this.onMessage = onMessage;
            this.inputQueue = inputQueue;
        }

        public async Task TryReceiveFrom()
        {
            MessageReadResult readResult;
            readResult = inputQueue.TryReceive();
            if (readResult.Successful)
            {
                var incomingMessage = readResult.Message;
                using (var bodyStream = incomingMessage.BodyStream)
                {
                    var pushContext = new PushContext(incomingMessage.MessageId, incomingMessage.Headers, bodyStream, new ContextBag());
                    await onMessage(pushContext).ConfigureAwait(false);
                }
            }
        }
    }
}