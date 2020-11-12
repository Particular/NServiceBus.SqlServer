namespace NServiceBus.Transport.SqlServer
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;

    abstract class ReceiveStrategy
    {
        protected TableBasedQueue InputQueue { get; private set; }
        protected TableBasedQueue ErrorQueue { get; private set; }

        Func<MessageContext, Task> onMessage;
        Func<ErrorContext, Task<ErrorHandleResult>> onError;

        protected ReceiveStrategy(TableBasedQueueCache tableBasedQueueCache)
        {
            this.tableBasedQueueCache = tableBasedQueueCache;
        }

        public void Init(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError)
        {
            InputQueue = inputQueue;
            ErrorQueue = errorQueue;

            this.onMessage = onMessage;
            this.onError = onError;
            this.criticalError = criticalError;
        }

        public abstract Task ReceiveMessage(CancellationTokenSource receiveCancellationTokenSource);

        protected Task ReleaseLease(Guid leaseId, SqlConnection connection, SqlTransaction transaction)
        {
            return InputQueue.ReleaseLease(leaseId, connection, transaction);
        }

        protected Task<bool> TryDeleteLeasedRow(Guid leaseId, SqlConnection connection, SqlTransaction transaction)
        {
            return InputQueue.TryDeleteLeasedRow(leaseId, connection, transaction);
        }

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
                if (await TryHandleDelayedMessage(receiveResult.Message, connection, transaction).ConfigureAwait(false))
                {
                    return null;
                }

                return receiveResult.Message;
            }
            receiveCancellationTokenSource.Cancel();
            return null;
        }

        protected async Task<bool> TryLeaseBasedMessagePreHandling(MessageReadResult receiveResult, SqlConnection connection, SqlTransaction transaction)
        {
            if (receiveResult.IsPoison)
            {
                await ErrorQueue.DeadLetter(receiveResult.PoisonMessage,  connection, transaction).ConfigureAwait(false);
                return true;
            }

            if (receiveResult.Message.Expired) //Do not process expired messages
            {
                return true;
            }

            if (await TryHandleDelayedMessage(receiveResult.Message, connection, transaction).ConfigureAwait(false))
            {
                return true;
            }

            return false;
        }

        protected async Task<bool> TryLeasedBasedProcessingMessage(Message message, TransportTransaction transportTransaction)
        {
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
            message.ResetHeaders();
            try
            {
                var errorContext = new ErrorContext(exception, message.Headers, message.TransportId, message.Body, transportTransaction, processingAttempts);
                errorContext.Message.Headers.Remove(ForwardHeader);

                return await onError(errorContext).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                criticalError.Raise($"Failed to execute recoverability policy for message with native ID: `{message.TransportId}`", ex);

                return ErrorHandleResult.RetryRequired;
            }
            finally
            {
                message.ResetHeaders();
            }
        }

        async Task<bool> TryHandleDelayedMessage(Message message, SqlConnection connection, SqlTransaction transaction)
        {
            if (message.Headers.TryGetValue(ForwardHeader, out var forwardDestination))
            {
                message.Headers.Remove(ForwardHeader);
            }

            if (forwardDestination == null)
            {
                //This is not a delayed message. Process in local endpoint instance.
                return false;
            }
            if (forwardDestination == InputQueue.Name)
            {
                //Do not forward the message. Process in local endpoint instance.
                return false;
            }
            var outgoingMessage = new OutgoingMessage(message.TransportId, message.Headers, message.Body);

            var destinationQueue = tableBasedQueueCache.Get(forwardDestination);
            await destinationQueue.Send(outgoingMessage, TimeSpan.MaxValue, connection, transaction).ConfigureAwait(false);
            return true;
        }

        const string ForwardHeader = "NServiceBus.SqlServer.ForwardDestination";
        TableBasedQueueCache tableBasedQueueCache;
        CriticalError criticalError;
    }
}