namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;

    abstract class ReceiveStrategy
    {
        protected TableBasedQueue InputQueue { get; private set; }
        protected TableBasedQueue ErrorQueue { get; private set; }

        Func<MessageContext, Task> onMessage;
        Func<ErrorContext, Task<ErrorHandleResult>> onError;

        public void Init(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError)
        {
            InputQueue = inputQueue;
            ErrorQueue = errorQueue;
            this.onMessage = onMessage;
            this.onError = onError;
            this.criticalError = criticalError;
        }

        public abstract Task ReceiveMessage(CancellationTokenSource receiveCancellationTokenSource);

        protected async Task<bool> TryProcessingMessage(Message message, Stream bodyStream, TransportTransaction transportTransaction)
        {
            using (var pushCancellationTokenSource = new CancellationTokenSource())
            {
                var body = await ReadStream(bodyStream).ConfigureAwait(false);
                var messageContext = new MessageContext(message.TransportId, message.Headers, body, transportTransaction, pushCancellationTokenSource, new ContextBag());

                await onMessage(messageContext).ConfigureAwait(false);

                // Cancellation is requested when message processing is aborted.
                // We return the opposite value:
                //  - true when message processing completed successfully,
                //  - false when message processing was aborted.
                return !pushCancellationTokenSource.Token.IsCancellationRequested;
            }
        }

        protected async Task<ErrorHandleResult> HandleError(Exception exception, Message message, TransportTransaction transportTransaction, int processingAttempts)
        {
            try
            {
                var body = await ReadStream(message.BodyStream).ConfigureAwait(false);
                var errorContext = new ErrorContext(exception, message.Headers, message.TransportId, body, transportTransaction, processingAttempts);

                return await onError(errorContext).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                criticalError.Raise($"Failed to execute reverability actions for message `{message.TransportId}`", ex);

                return ErrorHandleResult.RetryRequired;
            }
        }

        static async Task<byte[]> ReadStream(Stream bodyStream)
        {
            var body = new byte[bodyStream.Length];

            bodyStream.Seek(0, SeekOrigin.Begin);
            await bodyStream.ReadAsync(body, 0, body.Length).ConfigureAwait(false);

            return body;
        }

        CriticalError criticalError;
    }
}