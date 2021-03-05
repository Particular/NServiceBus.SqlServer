namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Extensibility;

    class ProcessWithTransactionScope : ReceiveStrategy
    {
        public ProcessWithTransactionScope(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory, FailureInfoStorage failureInfoStorage, TableBasedQueueCache tableBasedQueueCache)
         : base(tableBasedQueueCache)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
            this.failureInfoStorage = failureInfoStorage;
        }

        public override async Task ReceiveMessage(CancellationTokenSource stopBatch, CancellationToken cancellationToken = default)
        {
            Message message = null;
            var extensions = new ContextBag();

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

                    if (!await TryProcess(message, PrepareTransportTransaction(), extensions, cancellationToken).ConfigureAwait(false))
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
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, exception, extensions);
            }
        }

        TransportTransaction PrepareTransportTransaction()
        {
            var transportTransaction = new TransportTransaction();

            //these resources are meant to be used by anyone except message dispatcher e.g. persister
            transportTransaction.Set(Transaction.Current);

            return transportTransaction;
        }

        async Task<bool> TryProcess(Message message, TransportTransaction transportTransaction, ContextBag extensions, CancellationToken cancellationToken)
        {
            if (failureInfoStorage.TryGetFailureInfoForMessage(message.TransportId, out var failure))
            {
                var errorHandlingResult = await HandleError(failure.Exception, message, transportTransaction, failure.NumberOfProcessingAttempts, failure.Extensions, cancellationToken).ConfigureAwait(false);

                if (errorHandlingResult == ErrorHandleResult.Handled)
                {
                    return true;
                }
            }

            try
            {
                return await TryProcessingMessage(message, transportTransaction, extensions, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Graceful shutdown
                return false;
            }
            catch (Exception exception)
            {
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, exception, extensions);
                return false;
            }
        }

        TransactionOptions transactionOptions;
        SqlConnectionFactory connectionFactory;
        FailureInfoStorage failureInfoStorage;
    }
}