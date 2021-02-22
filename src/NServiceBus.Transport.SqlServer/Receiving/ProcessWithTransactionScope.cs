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

        public override async Task ReceiveMessage(CancellationTokenSource stopBatch, CancellationToken cancellationToken)
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

                    // DB-TODO: Passing token from source
                    if (!await TryProcess(message, PrepareTransportTransaction(), cancellationToken).ConfigureAwait(false))
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
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, exception);
            }
        }

        TransportTransaction PrepareTransportTransaction()
        {
            var transportTransaction = new TransportTransaction();

            //these resources are meant to be used by anyone except message dispatcher e.g. persister
            transportTransaction.Set(Transaction.Current);

            return transportTransaction;
        }

        async Task<bool> TryProcess(Message message, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            if (failureInfoStorage.TryGetFailureInfoForMessage(message.TransportId, out var failure))
            {
                var errorHandlingResult = await HandleError(failure.Exception, message, transportTransaction, failure.NumberOfProcessingAttempts, cancellationToken).ConfigureAwait(false);

                if (errorHandlingResult == ErrorHandleResult.Handled)
                {
                    return true;
                }
            }

            try
            {
                return await TryProcessingMessage(message, transportTransaction, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, exception);
                return false;
            }
        }

        TransactionOptions transactionOptions;
        SqlConnectionFactory connectionFactory;
        FailureInfoStorage failureInfoStorage;
    }
}