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
                MessageReadResult receiveResult;
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    receiveResult = await InputQueue.TryReceive(connection, transaction).ConfigureAwait(false);

                    if (receiveResult == MessageReadResult.NoMessage)
                    {
                        receiveCancellationTokenSource.Cancel();
                        return;
                    }

                    if (receiveResult.IsPoison)
                    {
                        await ErrorQueue.DeadLetter(receiveResult.PoisonMessage, connection, transaction).ConfigureAwait(false);
                        transaction.Commit();
                        return;
                    }

                    if (await TryHandleDelayedMessage(receiveResult.Message, connection, transaction).ConfigureAwait(false))
                    {
                        transaction.Commit();
                        return;
                    }

                    transaction.Commit();
                }

                var transportTransaction = new TransportTransaction();
                transportTransaction.Set(SettingsKeys.TransportTransactionSqlConnectionKey, connection);

                try
                {
                    await TryProcessingMessage(receiveResult.Message, transportTransaction).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Since this is TransactionMode.None, we don't care whether error handling says handled or retry. Message is gone either way.
                    _ = await HandleError(ex, receiveResult.Message, transportTransaction, 1).ConfigureAwait(false);
                }
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}