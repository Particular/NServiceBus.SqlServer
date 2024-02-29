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
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using Unicast.Queuing;
    using Faults;


    abstract class ProcessStrategy
    {
        protected TableBasedQueue InputQueue;
        protected TableBasedQueue ErrorQueue;

        OnMessage onMessage;
        OnError onError;

        protected ProcessStrategy(TableBasedQueueCache tableBasedQueueCache)
        {
            this.tableBasedQueueCache = tableBasedQueueCache;
            log = LogManager.GetLogger(GetType());
        }

        public void Init(TableBasedQueue inputQueue, TableBasedQueue errorQueue, OnMessage onMessage, OnError onError, Action<string, Exception, CancellationToken> criticalError)
        {
            InputQueue = inputQueue;
            ErrorQueue = errorQueue;

            this.onMessage = onMessage;
            this.onError = onError;
            this.criticalError = criticalError;
        }

        public abstract Task ProcessMessage(CancellationTokenSource stopBatchCancellationTokenSource, CancellationToken cancellationToken = default);

        protected async Task<bool> TryHandleMessage(Message message, TransportTransaction transportTransaction, ContextBag context, CancellationToken cancellationToken = default)
        {
            //Do not process expired messages
            if (message.Expired == false)
            {
                var messageContext = new MessageContext(message.TransportId, message.Headers, message.Body, transportTransaction, InputQueue.Name, context);
                await onMessage(messageContext, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        protected async Task<ErrorHandleResult> HandleError(Exception exception, Message message, TransportTransaction transportTransaction, int processingAttempts, ContextBag context, CancellationToken cancellationToken = default)
        {
            message.ResetHeaders();
            try
            {
                var errorContext = new ErrorContext(exception, message.Headers, message.TransportId, message.Body, transportTransaction, processingAttempts, InputQueue.Name, context);
                errorContext.Message.Headers.Remove(ForwardHeader);

                return await onError(errorContext, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                criticalError($"Failed to execute recoverability policy for message with native ID: `{message.TransportId}`", ex, cancellationToken);

                return ErrorHandleResult.RetryRequired;
            }
            finally
            {
                message.ResetHeaders();
            }
        }

        protected async Task<bool> TryHandleDelayedMessage(Message message, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
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
            var destinationQueue = tableBasedQueueCache.Get(forwardDestination);
            var outgoingMessage = new OutgoingMessage(message.TransportId, message.Headers, message.Body);
            try
            {
                await destinationQueue.Send(outgoingMessage, TimeSpan.MaxValue, connection, transaction, cancellationToken).ConfigureAwait(false);
            }
            catch (QueueNotFoundException e)
            {
                var hasEnclosedMessageTypeHeader = message.Headers.TryGetValue(Headers.EnclosedMessageTypes,
                    out var enclosedMessageTypeHeader);

                log.Error(
                    hasEnclosedMessageTypeHeader
                        ? $"Message with ID '{message.TransportId}' of type '{enclosedMessageTypeHeader}' cannot be forwarded to its destination queue '{e.Queue}' because it does not exist."
                        : $"Message with ID '{message.TransportId}' cannot be forwarded to its destination queue '{e.Queue}' because it does not exist.");

                ExceptionHeaderHelper.SetExceptionHeaders(outgoingMessage.Headers, e);
                outgoingMessage.Headers.Add(FaultsHeaderKeys.FailedQ, forwardDestination);
                await ErrorQueue.Send(outgoingMessage, TimeSpan.MaxValue, connection, transaction, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        const string ForwardHeader = "NServiceBus.SqlServer.ForwardDestination";
        TableBasedQueueCache tableBasedQueueCache;
        Action<string, Exception, CancellationToken> criticalError;
        protected ILog log;
    }
}