namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;

    class ReceiveWithTransactionScope : ReceiveStrategy
    {
        public ReceiveWithTransactionScope(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory, FailureInfoStorage failureInfoStorage)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
            this.failureInfoStorage = failureInfoStorage;
        }

        public override async Task ReceiveMessage(CancellationTokenSource receiveCancellationTokenSource)
        {
            Message message = null;
            try
            {
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                    message = await TryReceive(connection, null, receiveCancellationTokenSource).ConfigureAwait(false);

                    if (message == null)
                    {
                        // The message was received but is not fit for processing (e.g. was DLQd).
                        // In such a case we still need to commit the transport tx to remove message
                        // from the queue table.
                        scope.Complete();
                        return;
                    }

                    connection.Close();

                    if (await TryProcess(message, PrepareTransportTransaction()).ConfigureAwait(false))
                    {
                        scope.Complete();
                    }
                }
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

            //those resources are meant to be used by anyone except message dispatcher e.g. persister
            transportTransaction.Set(Transaction.Current);

            return transportTransaction;
        }

        async Task<bool> TryProcess(Message message, TransportTransaction transportTransaction)
        {
            FailureInfoStorage.ProcessingFailureInfo failure;
            if (failureInfoStorage.TryGetFailureInfoForMessage(message.TransportId, out failure))
            {
                var errorHandlingResult = await HandleError(failure.Exception, message, transportTransaction, failure.NumberOfProcessingAttempts).ConfigureAwait(false);

                if (errorHandlingResult == ErrorHandleResult.Handled)
                {
                    failureInfoStorage.ClearFailureInfoForMessage(message.TransportId);
                    return true;
                }
            }

            var messageProcessed = await TryProcessingMessage(message, transportTransaction).ConfigureAwait(false);
            if (messageProcessed)
            {
                failureInfoStorage.ClearFailureInfoForMessage(message.TransportId);
            }
            return messageProcessed;
        }

        TransactionOptions transactionOptions;
        SqlConnectionFactory connectionFactory;
        FailureInfoStorage failureInfoStorage;
    }
}