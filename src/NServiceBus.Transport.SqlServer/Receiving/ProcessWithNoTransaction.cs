namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;

    class ProcessWithNoTransaction : ReceiveStrategy
    {
        public ProcessWithNoTransaction(SqlConnectionFactory connectionFactory, TableBasedQueueCache tableBasedQueueCache)
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

                try
                {
                    // DB-TODO: Passing token from source
                    await TryProcessingMessage(message, transportTransaction, receiveCancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    // DB-TODO: Passing token from source
                    await HandleError(exception, message, transportTransaction, 1, receiveCancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}