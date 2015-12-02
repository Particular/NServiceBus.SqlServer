using System;
using System.Threading.Tasks;

namespace NServiceBus.Transports.SQLServer
{
    using System.Transactions;
    using NServiceBus.Extensibility;

    class ReceiveWithTransactionScope : ReceiveStrategy
    {
        public ReceiveWithTransactionScope(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
            using (var sqlConnection = await connectionFactory.OpenNewConnection())
            {
                var readResult = await inputQueue.TryReceive(sqlConnection, null).ConfigureAwait(false);

                if (readResult.IsPoison)
                {
                    await errorQueue.SendRawMessage(readResult.DataRecord, sqlConnection, null).ConfigureAwait(false);
                }
                else if (readResult.Successful)
                {
                    var message = readResult.Message;

                    using (var bodyStream = message.BodyStream)
                    {
                        var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, new NullTransaction(), new ContextBag());
                        pushContext.Context.Set(new ReceiveContext {Type = ReceiveType.TransactionScope, Connection = sqlConnection});

                        await onMessage(pushContext).ConfigureAwait(false);
                    }
                }

                scope.Complete();
            }
        }

        readonly TransactionOptions transactionOptions;
        readonly SqlConnectionFactory connectionFactory;
    }
}
