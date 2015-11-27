using System;
using System.Threading.Tasks;

namespace NServiceBus.Transports.SQLServer
{
    using System.Data.SqlClient;
    using System.Transactions;
    using NServiceBus.Extensibility;

    class ReceiveWithTransactionScope : ReceiveStrategy
    {
        public ReceiveWithTransactionScope(TransactionOptions transactionOptions, string connectionString)
        {
            this.transactionOptions = transactionOptions;
            this.connectionString = connectionString;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
            using (var sqlConnection = new SqlConnection(this.connectionString))
            {
                await sqlConnection.OpenAsync().ConfigureAwait(false);

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
                        //TODO: pass proper transaction
                        var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, new NullTransaction(), new ContextBag());
                        pushContext.Context.Set(new ReceiveContext {Type = ReceiveType.TransactionScope, Connection = sqlConnection});

                        await onMessage(pushContext).ConfigureAwait(false);
                    }
                }

                scope.Complete();
            }
        }

        TransactionOptions transactionOptions;
        string connectionString;
    }
}
