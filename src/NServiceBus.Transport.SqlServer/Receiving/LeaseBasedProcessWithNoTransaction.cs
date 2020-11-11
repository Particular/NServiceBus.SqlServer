namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class LeaseBasedProcessWithNoTransaction : ReceiveStrategy
    {
        public LeaseBasedProcessWithNoTransaction(SqlConnectionFactory connectionFactory, TableBasedQueueCache tableBasedQueueCache)
            : base(tableBasedQueueCache)
        {
            this.connectionFactory = connectionFactory;
        }

        public override async Task ReceiveMessage(CancellationTokenSource receiveCancellationTokenSource)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                Message message;
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    message = await TryReceive(connection, transaction, receiveCancellationTokenSource).ConfigureAwait(false);
                    transaction.Commit();
                }

                if (message == null)
                {
                    return;
                }

                var transportTransaction = new TransportTransaction();
                transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, connection);

                var processed = false;

                try
                {
                    if (await TryProcessingMessage(message, transportTransaction).ConfigureAwait(false))
                    {
                        using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
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
                catch (Exception processingException)
                {
                    var errorHandlingResult = await HandleError(processingException, message, transportTransaction, message.DequeueCount).ConfigureAwait(false);
                    if (errorHandlingResult == ErrorHandleResult.Handled)
                    {
                        using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
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
                finally
                {
                    if (processed == false)
                    {
                        await TryReleaseLease(message).ConfigureAwait(false);
                    }
                }
            }
        }

        async Task TryReleaseLease(Message message)
        {
            try
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
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

        SqlConnectionFactory connectionFactory;
        static ILog Logger = LogManager.GetLogger<LeaseBasedProcessWithNoTransaction>();
    }
}