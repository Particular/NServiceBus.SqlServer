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
    using Logging;
    using IsolationLevel = System.Data.IsolationLevel;

    class LeaseBasedProcessWithNativeTransaction : ReceiveStrategy
    {
        public LeaseBasedProcessWithNativeTransaction(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory, TableBasedQueueCache tableBasedQueueCache, bool transactionForReceiveOnly = false)
        : base(tableBasedQueueCache)
        {
            this.connectionFactory = connectionFactory;
            this.transactionForReceiveOnly = transactionForReceiveOnly;

            isolationLevel = IsolationLevelMapper.Map(transactionOptions.IsolationLevel);
        }

        public override async Task ReceiveMessage(CancellationTokenSource receiveCancellationTokenSource)
        {
            Message message;
            try
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    message = await TryReceive(connection, transaction, receiveCancellationTokenSource).ConfigureAwait(false);

                    transaction.Commit();

                    if (message == null)
                    {
                        // The message was received but is not fit for processing (e.g. was DLQd).
                        // In such a case we still need to commit the transport tx to remove message
                        // from the queue table.
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Warn("Message receive query failed.", exception);
                return;
            }

            var processed = false;

            try
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction(isolationLevel))
                {
                    var transportTransaction = PrepareTransportTransaction(connection, transaction);

                    if (await TryProcessingMessage(message, transportTransaction).ConfigureAwait(false))
                    {
                        if (await TryDeleteLeasedRow(message.LeaseId.Value, connection, transaction).ConfigureAwait(false))
                        {
                            transaction.Commit();
                            processed = true;
                        }
                        else
                        {
                            transaction.Rollback();
                        }
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                }
            }
            catch (Exception processingException)
            {
                try
                {
                    using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                    using (var transaction = connection.BeginTransaction(isolationLevel))
                    {
                        var transportTransaction = PrepareTransportTransaction(connection, transaction);

                        var errorHandlingResult = await HandleError(processingException, message, transportTransaction, message.DequeueCount).ConfigureAwait(false);

                        if (errorHandlingResult == ErrorHandleResult.Handled)
                        {
                            if (await TryDeleteLeasedRow(message.LeaseId.Value, connection, transaction).ConfigureAwait(false))
                            {
                                transaction.Commit();
                                processed = true;
                            }
                            else
                            {
                                transaction.Rollback();
                            }
                        }
                    }
                }
                catch (Exception errorHandlingException)
                {
                    Logger.Warn($"Error handling failed for message {message.TransportId}", errorHandlingException);
                }
            }
            finally
            {
                if (processed == false)
                {
                    await TryReleaseLease(message).ConfigureAwait(false);
                }
            }
        }

        async Task TryReleaseLease(Message message)
        {
            try
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction(isolationLevel))
                {
                    await ReleaseLease(message.LeaseId.Value, connection, transaction).ConfigureAwait(false);

                    transaction.Commit();
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"Failed to release message lock {message.TransportId}", e);
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

        IsolationLevel isolationLevel;
        SqlConnectionFactory connectionFactory;
        bool transactionForReceiveOnly;
        internal static string ReceiveOnlyTransactionMode = "SqlTransport.ReceiveOnlyTransactionMode";

        static ILog Logger = LogManager.GetLogger<LeaseBasedProcessWithNativeTransaction>();
    }
}