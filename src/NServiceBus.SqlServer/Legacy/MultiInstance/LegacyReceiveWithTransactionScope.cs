namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Transports;

    class LegacyReceiveWithTransactionScope : ReceiveStrategy
    {
        public LegacyReceiveWithTransactionScope(TransactionOptions transactionOptions, LegacySqlConnectionFactory connectionFactory)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, CancellationTokenSource receiveCancellationTokenSource, Func<PushContext, Task> onMessage)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))

            using (var inputConnection = await connectionFactory.OpenNewConnection(inputQueue.TransportAddress).ConfigureAwait(false))
            {
                var readResult = await inputQueue.TryReceive(inputConnection, null).ConfigureAwait(false);

                if (readResult.IsPoison)
                {
                    using (var errorConnection = await connectionFactory.OpenNewConnection(errorQueue.TransportAddress).ConfigureAwait(false))
                    {
                        await errorQueue.DeadLetter(readResult.PoisonMessage, errorConnection, null).ConfigureAwait(false);

                        scope.Complete();
                        return;
                    }
                }

                if (!readResult.Successful)
                {
                    scope.Complete();
                    receiveCancellationTokenSource.Cancel();
                    return;
                }

                var message = readResult.Message;

                        var context = new ContextBag();
                        context.Set<IDispatchStrategy>(new LegacyNonIsolatedDispatchStrategy(connectionFactory));

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
        LegacySqlConnectionFactory connectionFactory;
    }
}