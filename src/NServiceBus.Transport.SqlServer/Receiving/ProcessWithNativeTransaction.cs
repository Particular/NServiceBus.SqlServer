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
    using System.Transactions;
    using IsolationLevel = System.Data.IsolationLevel;
    using NServiceBus.Extensibility;

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

        public override async Task ReceiveMessage(CancellationTokenSource stopBatch, CancellationToken cancellationToken = default)
        {
            Message message = null;
            var context = new ContextBag();

            try
            {
                using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction(isolationLevel))
                {
                    message = await TryReceive(connection, transaction, stopBatch, cancellationToken).ConfigureAwait(false);

                    if (message == null)
                    {
                        // The message was received but is not fit for processing (e.g. was DLQd).
                        // In such a case we still need to commit the transport tx to remove message
                        // from the queue table.
                        transaction.Commit();
                        return;
                    }

                    if (!await TryProcess(message, PrepareTransportTransaction(connection, transaction), context, cancellationToken).ConfigureAwait(false))
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
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, exception, context);
            }
        }

        TransportTransaction PrepareTransportTransaction(SqlConnection connection, SqlTransaction transaction)
        {
            var transportTransaction = new TransportTransaction();

            //these resources are meant to be used by anyone except message dispatcher e.g. persister
            transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, connection);
            transportTransaction.Set(SettingsKeys.TransportTransactionSqlTransactionKey, transaction);

            if (transactionForReceiveOnly)
            {
                //this indicates to MessageDispatcher that it should not reuse connection or transaction for sends
                transportTransaction.Set(ReceiveOnlyTransactionMode, true);
            }

            return transportTransaction;
        }

        async Task<bool> TryProcess(Message message, TransportTransaction transportTransaction, ContextBag context, CancellationToken cancellationToken)
        {
            if (failureInfoStorage.TryGetFailureInfoForMessage(message.TransportId, out var failure))
            {
                var errorHandlingResult = await HandleError(failure.Exception, message, transportTransaction, failure.NumberOfProcessingAttempts, failure.Context, cancellationToken).ConfigureAwait(false);

                if (errorHandlingResult == ErrorHandleResult.Handled)
                {
                    return true;
                }
            }

            try
            {
                return await TryProcessingMessage(message, transportTransaction, context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Graceful shutdown
                return false;
            }
            catch (Exception exception)
            {
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, exception, context);
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