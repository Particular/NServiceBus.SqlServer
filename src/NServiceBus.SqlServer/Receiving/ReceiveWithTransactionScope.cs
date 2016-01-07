namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Extensibility;

    class ReceiveWithTransactionScope : ReceiveStrategy
    {
        public ReceiveWithTransactionScope(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory, EndpointConnectionStringLookup endpointConnectionStringLookup)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
            this.endpointConnectionStringLookup = endpointConnectionStringLookup;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, CancellationTokenSource cancellationTokenSource, Func<PushContext, Task> onMessage)
        {
            var connectionString = endpointConnectionStringLookup.ConnectionStringLookup(inputQueue.ToString()).GetAwaiter().GetResult();
            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
            using (var sqlConnection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                var readResult = await inputQueue.TryReceive(inputConnection, null).ConfigureAwait(false);

                if (readResult.IsPoison)
                {
                    var errorConnectionString = endpointConnectionStringLookup.ConnectionStringLookup(errorQueue.ToString()).GetAwaiter().GetResult();

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
                    transportTransaction.Set(sqlConnection);
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

        TransactionOptions transactionOptions;
        SqlConnectionFactory connectionFactory;
        readonly EndpointConnectionStringLookup endpointConnectionStringLookup;
    }
}
