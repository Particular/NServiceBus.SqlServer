namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;

    class ProcessWithNoTransaction : ReceiveStrategy
    {
        public ProcessWithNoTransaction(SqlConnectionFactory connectionFactory, TableBasedQueueCache tableBasedQueueCache)
        : base(tableBasedQueueCache)
        {
            this.connectionFactory = connectionFactory;
        }

        public override async Task ReceiveMessage(CancellationTokenSource stopBatch, CancellationToken cancellationToken = default)
        {
            using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
                Message message;
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    message = await TryReceive(connection, transaction, stopBatch, cancellationToken).ConfigureAwait(false);
                    transaction.Commit();
                }

                if (message == null)
                {
                    return;
                }

                var extensions = new ContextBag();
                var transportTransaction = new TransportTransaction();
                transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, connection);

                try
                {
                    await TryProcessingMessage(message, transportTransaction, extensions, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Graceful shutdown
                }
                catch (Exception exception)
                {
                    _ = await HandleError(exception, message, transportTransaction, 1, extensions, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}