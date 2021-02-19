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

    abstract class ReceiveStrategy
    {
        TableBasedQueue inputQueue;
        TableBasedQueue errorQueue;

        OnMessage onMessage;
        OnError onError;
        OnCompleted onCompleted;

        protected ReceiveStrategy(TableBasedQueueCache tableBasedQueueCache)
        {
            this.tableBasedQueueCache = tableBasedQueueCache;
        }

        public void Init(TableBasedQueue inputQueue, TableBasedQueue errorQueue, OnMessage onMessage, OnError onError, OnCompleted onCompleted, Action<string, Exception, CancellationToken> criticalError)
        {
            this.inputQueue = inputQueue;
            this.errorQueue = errorQueue;

            this.onMessage = onMessage;
            this.onError = onError;
            this.onCompleted = onCompleted;
            this.criticalError = criticalError;
        }

        public abstract Task ReceiveMessage(ReceiveContext receiveContext, CancellationTokenSource stopBatch, CancellationToken cancellationToken = default);

        protected async Task<Message> TryReceive(SqlConnection connection, SqlTransaction transaction, CancellationTokenSource stopBatch, CancellationToken cancellationToken = default)
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

            stopBatch.Cancel();
            return null;
        }

        protected async Task<bool> TryProcessingMessage(Message message, ReceiveContext receiveContext, TransportTransaction transportTransaction, CancellationToken cancellationToken = default)
        {
            //Do not process expired messages
            if (message.Expired == false)
            {
                var messageContext = new MessageContext(message.TransportId, message.Headers, message.Body, transportTransaction, receiveContext.Extensions);
                await onMessage(messageContext, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        protected async Task<ErrorHandleResult> HandleError(ReceiveContext receiveContext, Exception exception, Message message, TransportTransaction transportTransaction, int processingAttempts, CancellationToken cancellationToken = default)
        {
            message.ResetHeaders();
            try
            {
                var errorContext = new ErrorContext(exception, message.Headers, message.TransportId, message.Body, transportTransaction, processingAttempts, receiveContext.Extensions);
                errorContext.Message.Headers.Remove(ForwardHeader);

                return await onError(errorContext, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return ErrorHandleResult.RetryRequired;
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

        protected Task MarkComplete(Message message, ReceiveContext receiveContext, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                return Task.CompletedTask;
            }

            try
            {
                var context = new CompleteContext(message.TransportId, receiveContext.WasAcknowledged, message.Headers, receiveContext.StartedAt, DateTimeOffset.UtcNow, receiveContext.OnMessageFailed, receiveContext.Extensions);
                return onCompleted(context, cancellationToken);
            }
            catch (Exception)
            {
            }

            return Task.CompletedTask;
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