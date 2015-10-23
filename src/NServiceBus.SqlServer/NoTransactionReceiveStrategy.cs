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