namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Extensibility;

    class ReceiveWithReceiveOnlyTransaction : ReceiveStrategy
    {
        internal static string ReceiveOnlyTransactionMode = "SqlTransport.ReceiveOnlyTransactionMode";

        public ReceiveWithReceiveOnlyTransaction(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;

            isolationLevel = IsolationLevelMapper.Map(transactionOptions.IsolationLevel);
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, CancellationTokenSource cancellationTokenSource, Func<PushContext, Task> onMessage)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                using (var transaction = connection.BeginTransaction(isolationLevel))
                {
                    try
                    {
                        var readResult = await inputQueue.TryReceive(connection, transaction).ConfigureAwait(false);

                        if (readResult.IsPoison)
                        {
                            await errorQueue.SendRawMessage(readResult.DataRecord, connection, transaction).ConfigureAwait(false);

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

                            //those resources are meant to be used by anyone except message dispatcher e.g. persister
                            transportTransaction.Set(connection);
                            transportTransaction.Set(transaction);

                            //this indicates to MessageDispatcher that it should not reuse connection or transaction for sends
                            transportTransaction.Set(ReceiveOnlyTransactionMode, true);

                            var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, transportTransaction, cancellationTokenSource, new ContextBag());

                            await onMessage(pushContext).ConfigureAwait(false);

                            if (cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                transaction.Rollback();
                                return;
                            }
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