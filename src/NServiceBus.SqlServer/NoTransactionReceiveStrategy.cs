namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;

    class NoTransactionReceiveStrategy
    {
        readonly TableBasedQueue inputQueue;
        readonly TableBasedQueue errorQueue;
        readonly Func<PushContext, Task> onMessage;

        public NoTransactionReceiveStrategy(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage)
        {
            this.inputQueue = inputQueue;
            this.errorQueue = errorQueue;
            this.onMessage = onMessage;
        }

        public async Task TryReceiveFrom()
        {
            MessageReadResult readResult;
            readResult = inputQueue.TryReceive();

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