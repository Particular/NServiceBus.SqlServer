namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;

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
                MessageReadResult receiveResult;
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    receiveResult = await InputQueue.TryReceive(connection, transaction, cancellationToken).ConfigureAwait(false);

                    if (receiveResult == MessageReadResult.NoMessage)
                    {
                        stopBatchCancellationTokenSource.Cancel();
                        return;
                    }

                    if (receiveResult.IsPoison)
                    {
                        await ErrorQueue.DeadLetter(receiveResult.PoisonMessage, connection, transaction, cancellationToken).ConfigureAwait(false);
                        transaction.Commit();
                        return;
                    }

                    if (await TryHandleDelayedMessage(receiveResult.Message, connection, transaction, cancellationToken).ConfigureAwait(false))
                    {
                        transaction.Commit();
                        return;
                    }

                    transaction.Commit();
                }

                var context = new ContextBag();
                var transportTransaction = new TransportTransaction();
                transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, connection);

                try
                {
                    await TryHandleMessage(receiveResult.Message, transportTransaction, context, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
                {
                    // Since this is TransactionMode.None, we don't care whether error handling says handled or retry. Message is gone either way.
                    _ = await HandleError(ex, receiveResult.Message, transportTransaction, 1, context, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}