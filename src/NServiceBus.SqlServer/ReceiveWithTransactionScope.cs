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
                    sqlConnection.Open();
                    var readResult = inputQueue.TryReceive(sqlConnection);
                    if (readResult.IsPoison)
                    {
                        errorQueue.Send(readResult.DataRecord, sqlConnection);
                        scope.Complete();
                        return;
                    }

                    if (readResult.Successful)
                    {
                        var message = readResult.Message;

                        using (var bodyStream = message.BodyStream)
                        {
                            bodyStream.Position = 0;
                            var pushContext = new PushContext(message.Id, message.Headers, bodyStream, new ContextBag());
                            await onMessage(pushContext).ConfigureAwait(false);

                            scope.Complete();
                            scope.Dispose(); //TODO: check if calling that is really necessary
                            // We explicitly calling Dispose so that we force any exception to not bubble, eg Concurrency/Deadlock exception.
                            return;
                        }
                    }

                    scope.Complete();
                }
            }
        }
    }
}
