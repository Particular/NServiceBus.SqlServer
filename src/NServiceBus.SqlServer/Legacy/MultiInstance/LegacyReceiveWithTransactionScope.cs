namespace NServiceBus.Transports.SQLServer.Legacy.MultiInstance
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;

    class LegacyReceiveWithTransactionScope : ReceiveStrategy
    {
        public LegacyReceiveWithTransactionScope(TransactionOptions transactionOptions, LegacySqlConnectionFactory connectionFactory)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
        }

        public async Task ReceiveMessage(ITableBasedQueue inputQueue, ITableBasedQueue errorQueue, CancellationTokenSource receiveCancellationTokenSource, Func<PushContext, Task> onMessage)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var inputConnection = await connectionFactory.OpenNewConnection(inputQueue.TransportAddress).ConfigureAwait(false))
                {
                    var readResult = await inputQueue.TryReceive(inputConnection, null).ConfigureAwait(false);

                    if (readResult.IsPoison)
                    {
                        using (var errorConnection = await connectionFactory.OpenNewConnection(errorQueue.TransportAddress).ConfigureAwait(false))
                        {
                            await errorQueue.SendRawMessage(readResult.DataRecord, errorConnection, null).ConfigureAwait(false);

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

                    using (var pushCancellationTokenSource = new CancellationTokenSource())
                    using (var bodyStream = message.BodyStream)
                    {
                        var transportTransaction = new TransportTransaction();
                        transportTransaction.Set(inputConnection);
                        transportTransaction.Set(Transaction.Current);

                        var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, transportTransaction, pushCancellationTokenSource, new ContextBag());

                        await onMessage(pushContext).ConfigureAwait(false);

                        if (pushCancellationTokenSource.Token.IsCancellationRequested)
                        {
                            return;
                        }
                    }

                    scope.Complete();
                }
            }
        }

        TransactionOptions transactionOptions;
        LegacySqlConnectionFactory connectionFactory;
    }
}