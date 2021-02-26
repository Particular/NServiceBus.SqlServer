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

        public override async Task ReceiveMessage(ReceiveContext receiveContext, CancellationTokenSource stopBatch, CancellationToken cancellationToken)
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

                var transportTransaction = new TransportTransaction();
                transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, connection);

                try
                {
                    await TryProcessingMessage(message, receiveContext, transportTransaction, cancellationToken).ConfigureAwait(false);
                    receiveContext.WasAcknowledged = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Graceful shutdown
                }
                catch (Exception exception)
                {
                    receiveContext.OnMessageFailed = true;
                    var result = await HandleError(receiveContext, exception, message, transportTransaction, 1, cancellationToken).ConfigureAwait(false);
                    if (result == ErrorHandleResult.Handled)
                    {
                        receiveContext.WasAcknowledged = true;
                    }
                }

                await MarkComplete(message, receiveContext, cancellationToken).ConfigureAwait(false);
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}