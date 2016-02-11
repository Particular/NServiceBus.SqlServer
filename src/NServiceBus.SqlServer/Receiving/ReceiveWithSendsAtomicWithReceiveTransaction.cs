namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Extensibility;

    class ReceiveWithSendsAtomicWithReceiveTransaction : ReceiveStrategy
    {
        public ReceiveWithSendsAtomicWithReceiveTransaction(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;

            isolationLevel = IsolationLevelMapper.Map(transactionOptions.IsolationLevel);
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, CancellationTokenSource cancellationTokenSource, Func<PushContext, Task> onMessage)
        {
            using (var sqlConnection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                using (var transaction = sqlConnection.BeginTransaction(isolationLevel))
                {
                    try
                    {
                        var readResult = await inputQueue.TryReceive(sqlConnection, transaction).ConfigureAwait(false);

                        if (readResult.IsPoison)
                        {
                            await errorQueue.SendRawMessage(readResult.DataRecord, sqlConnection, transaction).ConfigureAwait(false);

                            transaction.Commit();

                            return;
                        }

                        if (!readResult.Successful)
                        {
                            transaction.Commit();

                            return;
                        }

                        var message = readResult.Message;

                        using (var bodyStream = message.BodyStream)
                        {
                            var transportTransaction = new TransportTransaction();

                            transportTransaction.Set(sqlConnection);
                            transportTransaction.Set(transaction);

                            var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, transportTransaction, cancellationTokenSource, new ContextBag());

                            await onMessage(pushContext).ConfigureAwait(false);
                        }

                        if (cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            transaction.Rollback();
                            return;
                        }

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        System.Data.IsolationLevel isolationLevel;
        SqlConnectionFactory connectionFactory;
    }
}