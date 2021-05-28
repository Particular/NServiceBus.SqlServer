namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;

    class ProcessWithNoTransaction : ProcessStrategy
    {
        public ProcessWithNoTransaction(SqlConnectionFactory connectionFactory, TableBasedQueueCache tableBasedQueueCache)
        : base(tableBasedQueueCache)
        {
            this.connectionFactory = connectionFactory;
        }

        public override async Task ProcessMessage(CancellationTokenSource stopBatchCancellationTokenSource, CancellationToken cancellationToken = default)
        {
            using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
                Message message;
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    message = await TryGetMessage(connection, transaction, stopBatchCancellationTokenSource, cancellationToken).ConfigureAwait(false);
                    transaction.Commit();
                }

                if (message == null)
                {
                    return;
                }

                var context = new ContextBag();
                var transportTransaction = new TransportTransaction();
                transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, connection);

                try
                {
                    await TryHandleMessage(message, transportTransaction, context, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
                {
                    // Since this is TransactionMode.None, we don't care whether error handling says handled or retry. Message is gone either way.
                    _ = await HandleError(ex, message, transportTransaction, 1, context, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}