namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using IsolationLevel = System.Data.IsolationLevel;

    class ProcessWithNativeTransaction : ReceiveStrategy
    {
        public ProcessWithNativeTransaction(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory, FailureInfoStorage failureInfoStorage, TableBasedQueueCache tableBasedQueueCache, bool transactionForReceiveOnly = false)
        : base(tableBasedQueueCache)
        {
            this.connectionFactory = connectionFactory;
            this.failureInfoStorage = failureInfoStorage;
            this.transactionForReceiveOnly = transactionForReceiveOnly;

            isolationLevel = IsolationLevelMapper.Map(transactionOptions.IsolationLevel);
        }

        public override async Task ReceiveMessage(CancellationTokenSource receiveCancellationTokenSource)
        {
            Message message = null;
            try
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction(isolationLevel))
                {
                    message = await TryReceive(connection, transaction, receiveCancellationTokenSource).ConfigureAwait(false);

                    if (message == null)
                    {
                        // The message was received but is not fit for processing (e.g. was DLQd).
                        // In such a case we still need to commit the transport tx to remove message
                        // from the queue table.
                        transaction.Commit();
                        return;
                    }

                    if (!await TryProcess(message, PrepareTransportTransaction(connection, transaction)).ConfigureAwait(false))
                    {
                        transaction.Rollback();
                        return;
                    }

                    transaction.Commit();
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

        TransportTransaction PrepareTransportTransaction(SqlConnection connection, SqlTransaction transaction)
        {
            var transportTransaction = new TransportTransaction();

            //those resources are meant to be used by anyone except message dispatcher e.g. persister
            transportTransaction.Set(connection);
            transportTransaction.Set(transaction);

            if (transactionForReceiveOnly)
            {
                //this indicates to MessageDispatcher that it should not reuse connection or transaction for sends
                transportTransaction.Set(ReceiveOnlyTransactionMode, true);
            }

            return transportTransaction;
        }

        async Task<bool> TryProcess(Message message, TransportTransaction transportTransaction)
        {
            if (failureInfoStorage.TryGetFailureInfoForMessage(message.TransportId, out var failure))
            {
                var errorHandlingResult = await HandleError(failure.Exception, message, transportTransaction, failure.NumberOfProcessingAttempts).ConfigureAwait(false);

                if (errorHandlingResult == ErrorHandleResult.Handled)
                {
                    return true;
                }
            }

            try
            {
                return await TryProcessingMessage(message, transportTransaction).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, exception);
                return false;
            }
        }

        IsolationLevel isolationLevel;
        SqlConnectionFactory connectionFactory;
        FailureInfoStorage failureInfoStorage;
        bool transactionForReceiveOnly;
        internal static string ReceiveOnlyTransactionMode = "SqlTransport.ReceiveOnlyTransactionMode";
    }
}