namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;

    class ProcessWithTransactionScope : ReceiveStrategy
    {
        public ProcessWithTransactionScope(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory, FailureInfoStorage failureInfoStorage, TableBasedQueueCache tableBasedQueueCache)
         : base(tableBasedQueueCache)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
            this.failureInfoStorage = failureInfoStorage;
        }

        public override async Task ReceiveMessage(ReceiveContext receiveContext, CancellationTokenSource stopBatch, CancellationToken cancellationToken = default)
        {
            Message message = null;
            try
            {
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                {
                    message = await TryReceive(connection, null, stopBatch, cancellationToken).ConfigureAwait(false);

                    if (message == null)
                    {
                        // The message was received but is not fit for processing (e.g. was DLQd).
                        // In such a case we still need to commit the transport tx to remove message
                        // from the queue table.
                        scope.Complete();
                        return;
                    }

                    connection.Close();

                    if (await TryProcess(message, receiveContext, PrepareTransportTransaction(), cancellationToken).ConfigureAwait(false))
                    {
                        receiveContext.WasAcknowledged = true;
                    }
                    else
                    {
                        return;
                    }

                    scope.Complete();
                }

                failureInfoStorage.ClearFailureInfoForMessage(message.TransportId);
            }
            catch (Exception exception)
            {
                if (message == null)
                {
                    throw;
                }
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, exception, receiveContext);
            }

            await MarkComplete(message, receiveContext, cancellationToken).ConfigureAwait(false);
        }

        TransportTransaction PrepareTransportTransaction()
        {
            var transportTransaction = new TransportTransaction();

            //these resources are meant to be used by anyone except message dispatcher e.g. persister
            transportTransaction.Set(Transaction.Current);

            return transportTransaction;
        }

        async Task<bool> TryProcess(Message message, ReceiveContext receiveContext, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            if (failureInfoStorage.TryGetFailureInfoForMessage(message.TransportId, out var failure))
            {
                var errorHandlingResult = await HandleError(failure.ReceiveContext, failure.Exception, message, transportTransaction, failure.NumberOfProcessingAttempts, cancellationToken).ConfigureAwait(false);

                if (errorHandlingResult == ErrorHandleResult.Handled)
                {
                    failure.ReceiveContext.WasAcknowledged = true;
                }

                await MarkComplete(message, failure.ReceiveContext, cancellationToken).ConfigureAwait(false);

                if (errorHandlingResult == ErrorHandleResult.Handled)
                {
                    return true;
                }
            }

            try
            {
                return await TryProcessingMessage(message, receiveContext, transportTransaction, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Graceful shutdown
                await MarkComplete(message, receiveContext, cancellationToken).ConfigureAwait(false);
                return false;
            }
            catch (Exception exception)
            {
                receiveContext.OnMessageFailed = true;
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, exception, receiveContext);
                return false;
            }
        }

        TransactionOptions transactionOptions;
        SqlConnectionFactory connectionFactory;
        FailureInfoStorage failureInfoStorage;
    }
}