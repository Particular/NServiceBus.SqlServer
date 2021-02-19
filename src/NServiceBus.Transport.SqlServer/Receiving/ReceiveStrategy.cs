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
        TableBasedQueue inputQueue;
        TableBasedQueue errorQueue;

        OnMessage onMessage;
        OnError onError;

        protected ReceiveStrategy(TableBasedQueueCache tableBasedQueueCache)
        {
            this.tableBasedQueueCache = tableBasedQueueCache;
        }

        public void Init(TableBasedQueue inputQueue, TableBasedQueue errorQueue, OnMessage onMessage, OnError onError, Action<string, Exception, CancellationToken> criticalError)
        {
            this.inputQueue = inputQueue;
            this.errorQueue = errorQueue;

            this.onMessage = onMessage;
            this.onError = onError;
            this.criticalError = criticalError;
        }

        public abstract Task ReceiveMessage(CancellationToken cancellationToken = default);

        protected async Task<Message> TryReceive(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            var receiveResult = await inputQueue.TryReceive(connection, transaction, cancellationToken).ConfigureAwait(false);

            if (receiveResult.IsPoison)
            {
                await errorQueue.DeadLetter(receiveResult.PoisonMessage, connection, transaction, cancellationToken).ConfigureAwait(false);
                return null;
            }

            if (receiveResult.Successful)
            {
                if (await TryHandleDelayedMessage(receiveResult.Message, connection, transaction, cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                return receiveResult.Message;
            }
            return null;
        }

        protected async Task<bool> TryProcessingMessage(Message message, TransportTransaction transportTransaction, CancellationToken cancellationToken = default)
        {
            //Do not process expired messages
            if (message.Expired == false)
            {
                var messageContext = new MessageContext(message.TransportId, message.Headers, message.Body, transportTransaction, new ContextBag());
                await onMessage(messageContext, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        protected async Task<ErrorHandleResult> HandleError(Exception exception, Message message, TransportTransaction transportTransaction, int processingAttempts, CancellationToken cancellationToken = default)
        {
            message.ResetHeaders();
            try
            {
                var errorContext = new ErrorContext(exception, message.Headers, message.TransportId, message.Body, transportTransaction, processingAttempts);
                errorContext.Message.Headers.Remove(ForwardHeader);

                return await onError(errorContext, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                criticalError($"Failed to execute recoverability policy for message with native ID: `{message.TransportId}`", ex, cancellationToken);

                return ErrorHandleResult.RetryRequired;
            }
            finally
            {
                message.ResetHeaders();
            }
        }

        async Task<bool> TryHandleDelayedMessage(Message message, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
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
            if (forwardDestination == inputQueue.Name)
            {
                //Do not forward the message. Process in local endpoint instance.
                return false;
            }
            var outgoingMessage = new OutgoingMessage(message.TransportId, message.Headers, message.Body);

            var destinationQueue = tableBasedQueueCache.Get(forwardDestination);
            await destinationQueue.Send(outgoingMessage, TimeSpan.MaxValue, connection, transaction, cancellationToken).ConfigureAwait(false);
            return true;
        }

        const string ForwardHeader = "NServiceBus.SqlServer.ForwardDestination";
        TableBasedQueueCache tableBasedQueueCache;
        Action<string, Exception, CancellationToken> criticalError;
    }
}