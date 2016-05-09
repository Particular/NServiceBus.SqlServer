namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Transports;

    class ReceiveWithTransactionScope : ReceiveStrategy
    {
        public ReceiveWithTransactionScope(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, CancellationTokenSource receiveCancellationTokenSource, Func<PushContext, Task> onMessage)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                var readResult = await inputQueue.TryReceive(connection, null).ConfigureAwait(false);

                if (readResult.IsPoison)
                {
                    await errorQueue.DeadLetter(readResult.PoisonMessage, connection, null).ConfigureAwait(false);
                    scope.Complete();
                    return;
                }

                if (!readResult.Successful)
                {
                    scope.Complete();

                    receiveCancellationTokenSource.Cancel();

                    return;
                }

                var message = readResult.Message;

                using (var pushCancellationTokenSource = new CancellationTokenSource())
                using (var bodyStream = message.BodyStream)
                {
                    var transportTransaction = new TransportTransaction();
                    transportTransaction.Set(connection);
                    transportTransaction.Set(Transaction.Current);

                    var context = new ContextBag();
                    context.Set<IDispatchStrategy>(new ReceiveConnectionDispatchStrategy(connection, null));

                    var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, transportTransaction, pushCancellationTokenSource, context);

                    await onMessage(pushContext).ConfigureAwait(false);

                    if (pushCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        return;
                    }
                }

                scope.Complete();
            }
        }

        TransactionOptions transactionOptions;
        SqlConnectionFactory connectionFactory;
    }
}