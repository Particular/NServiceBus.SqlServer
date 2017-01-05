namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;

    class LegacyReceiveWithTransactionScope : ReceiveStrategy
    {
        public LegacyReceiveWithTransactionScope(TransactionOptions transactionOptions, LegacySqlConnectionFactory connectionFactory, FailureInfoStorage failureInfoStorage)
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
                using (var inputConnection = await connectionFactory.OpenNewConnection(InputQueue.TransportAddress).ConfigureAwait(false))
                {
                    message = await TryReceive(inputConnection, null, receiveCancellationTokenSource).ConfigureAwait(false);

                    if (message == null)
                    {
                        // The message was received but is not fit for processing (e.g. was DLQd).
                        // In such a case we still need to commit the transport tx to remove message
                        // from the queue table.
                        scope.Complete();
                        return;
                    }

                    if (await TryProcess(message, PrepareTransportTransaction(inputConnection)).ConfigureAwait(false))
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
        
        protected override async Task DeadLetter(MessageReadResult receiveResult, SqlConnection connection, SqlTransaction transaction)
        {
            using (var errorConnection = await connectionFactory.OpenNewConnection(ErrorQueue.TransportAddress).ConfigureAwait(false))
            {
                await ErrorQueue.DeadLetter(receiveResult.PoisonMessage, errorConnection, null).ConfigureAwait(false);
            }
        }

        TransportTransaction PrepareTransportTransaction(SqlConnection connection)
        {
            var transportTransaction = new TransportTransaction();

            //those resources are meant to be used by anyone except message dispatcher e.g. persister
            transportTransaction.Set(connection);
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
        LegacySqlConnectionFactory connectionFactory;
        FailureInfoStorage failureInfoStorage;
    }
}