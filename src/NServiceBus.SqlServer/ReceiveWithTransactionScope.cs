using System;
using System.Threading.Tasks;

namespace NServiceBus.Transports.SQLServer
{
    using System.Data.SqlClient;
    using System.Transactions;
    using NServiceBus.Extensibility;

    class ReceiveWithTransactionScope : ReceiveStrategy
    {
        TransactionOptions transactionOptions;
        readonly string connectionString;

        public ReceiveWithTransactionScope(TransactionOptions transactionOptions, string connectionString)
        {
            this.transactionOptions = transactionOptions;
            this.connectionString = connectionString;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var sqlConnection = new SqlConnection(this.connectionString))
                {
                    await sqlConnection.OpenAsync().ConfigureAwait(false);

                    var readResult = await inputQueue.TryReceive(sqlConnection, null).ConfigureAwait(false);

                    if (readResult.IsPoison)
                    {
                        await errorQueue.SendRawMessage(readResult.DataRecord, sqlConnection, null).ConfigureAwait(false);
                        scope.Complete();
                        return;
                    }

                    if (readResult.Successful)
                    {
                        var message = readResult.Message;

                        using (var bodyStream = message.BodyStream)
                        {
                            var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, new ContextBag());
                            pushContext.Context.Set(new ReceiveContext {Type = ReceiveType.TransactionScope, Connection = sqlConnection});

                            await onMessage(pushContext).ConfigureAwait(false);

                            scope.Complete();
                            // Try finally?
                            scope.Dispose(); //TODO: check if calling that is really necessary
                                             // We explicitly calling Dispose so that we force any exception to not bubble, eg Concurrency/Deadlock exception.

                            return;
                        }
                    }
                    // Try finally?
                    scope.Complete();
                }
            }
        }
    }
}
