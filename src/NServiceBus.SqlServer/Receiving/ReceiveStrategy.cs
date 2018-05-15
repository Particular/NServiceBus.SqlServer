namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
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

        protected async Task<Message> TryReceive(SqlConnection connection, SqlTransaction transaction, CancellationTokenSource receiveCancellationTokenSource)
        {
            var receiveResult = await InputQueue.TryReceive(connection, transaction).ConfigureAwait(false);

            if (receiveResult.IsPoison)
            {
                await ErrorQueue.DeadLetter(receiveResult.PoisonMessage, connection, transaction).ConfigureAwait(false);
                return null;
            }

            if (receiveResult.Successful)
            {
                return receiveResult.Message;
            }
            receiveCancellationTokenSource.Cancel();
            return null;
        }

        protected async Task<bool> TryProcessingMessage(Message message, TransportTransaction transportTransaction)
        {
            if (message.Expired) //Do not process expired messages
            {
                return true;
            }
            using (var pushCancellationTokenSource = new CancellationTokenSource())
            {
                var messageContext = new MessageContext(message.TransportId, message.Headers, message.Body, transportTransaction, pushCancellationTokenSource, new ContextBag());

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
                var errorContext = new ErrorContext(exception, message.Headers, message.TransportId, message.Body, transportTransaction, processingAttempts);

                return await onError(errorContext).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                criticalError.Raise($"Failed to execute reverability actions for message `{message.TransportId}`", ex);

                return ErrorHandleResult.RetryRequired;
            }
        }

        CriticalError criticalError;
    }
}