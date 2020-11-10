namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Logging;

    class LeaseBasedProcessWithTransactionScope : ReceiveStrategy
    {
        public LeaseBasedProcessWithTransactionScope(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory, TableBasedQueueCache tableBasedQueueCache)
         : base(tableBasedQueueCache)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
        }

        public override async Task ReceiveMessage(CancellationTokenSource receiveCancellationTokenSource)
        {
            Message message;

            try
            {
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                    message = await TryReceive(connection, null, receiveCancellationTokenSource).ConfigureAwait(false);

                    scope.Complete();

                    if (message == null)
                    {
                        // The message was received but is not fit for processing (e.g. was DLQd).
                        // In such a case we still need to commit the transport tx to remove message
                        // from the queue table.
                        return;
                    }

                    connection.Close();
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
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (await TryProcessingMessage(message, PrepareTransportTransaction()).ConfigureAwait(false))
                    {
                        using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                        {
                            if (await TryDeleteLeasedRow(message.LeaseId.Value, connection, null).ConfigureAwait(false))
                            {
                                scope.Complete();
                                processed = true;
                            }
                        }
                    }
                }
            }
            catch (Exception processingException)
            {
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                {
                    var errorHandlingResult = await HandleError(processingException, message, PrepareTransportTransaction(), message.DequeueCount).ConfigureAwait(false);

                    if (errorHandlingResult == ErrorHandleResult.Handled)
                    {
                        using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                        {
                            if (await TryDeleteLeasedRow(message.LeaseId.Value, connection, null).ConfigureAwait(false))
                            {
                                scope.Complete();
                                processed = true;
                            }
                        }
                    }
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
                using (var releaseScope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                    await ReleaseLease(message.LeaseId.Value, connection, null).ConfigureAwait(false);

                    releaseScope.Complete();
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"Failed to release message lock {message.TransportId}", e);
            }
        }

        TransportTransaction PrepareTransportTransaction()
        {
            var transportTransaction = new TransportTransaction();

            //these resources are meant to be used by anyone except message dispatcher e.g. persister
            transportTransaction.Set(Transaction.Current);

            return transportTransaction;
        }

        TransactionOptions transactionOptions;
        SqlConnectionFactory connectionFactory;

        static ILog Logger = LogManager.GetLogger<LeaseBasedProcessWithTransactionScope>();
    }
}