namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;

    class ReceiveWithTransactionScope : ReceiveStrategy
    {
        public ReceiveWithTransactionScope(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, CancellationTokenSource cancellationTokenSource, Func<PushContext, Task> onMessage)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                    var readResult = await inputQueue.TryReceive(connection, null).ConfigureAwait(false);

                    if (readResult.IsPoison)
                    {
                        await errorQueue.SendRawMessage(readResult.DataRecord, connection, null).ConfigureAwait(false);

                        scope.Complete();
                        return;
                    }

                    if (!readResult.Successful)
                    {
                        scope.Complete();

                        return;
                    }

                    var message = readResult.Message;

                    using (var bodyStream = message.BodyStream)
                    {
                        var transportTransaction = new TransportTransaction();
                        transportTransaction.Set(connection);
                        transportTransaction.Set(Transaction.Current);

                        var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, transportTransaction, cancellationTokenSource, new ContextBag());

                        await onMessage(pushContext).ConfigureAwait(false);
                    }

                    if (cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    scope.Complete();
                }
            }
        }

        TransactionOptions transactionOptions;
        SqlConnectionFactory connectionFactory;
    }
}