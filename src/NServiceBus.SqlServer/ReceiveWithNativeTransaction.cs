namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Extensibility;

    class ReceiveWithNativeTransaction : ReceiveStrategy
    {

        public ReceiveWithNativeTransaction(TransactionOptions transactionOptions, string connectionString)
        {
            this.isolationLevel = GetSqlIsolationLevel(transactionOptions.IsolationLevel);
            this.connectionString = connectionString;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                await sqlConnection.OpenAsync().ConfigureAwait(false);

                using (var transaction = sqlConnection.BeginTransaction(isolationLevel))
                {
                    try
                    {
                        var readResult = await inputQueue.TryReceive(sqlConnection, transaction).ConfigureAwait(false);

                        if (readResult.IsPoison)
                        {
                            await errorQueue.SendRawMessage(readResult.DataRecord, sqlConnection, transaction).ConfigureAwait(false);
                            transaction.Commit();
                            return;
                        }

                        if (readResult.Successful)
                        {
                            var message = readResult.Message;

                            using (var bodyStream = message.BodyStream)
                            {
                                var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, new ContextBag());
                                pushContext.Context.Set(new ReceiveContext {Type = ReceiveType.NativeTransaction, Transaction = transaction});

                                await onMessage(pushContext).ConfigureAwait(false);

                                transaction.Commit();

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

        static System.Data.IsolationLevel GetSqlIsolationLevel(IsolationLevel isolationLevel)
        {
            switch (isolationLevel)
            {
                case IsolationLevel.Serializable:
                    return System.Data.IsolationLevel.Serializable;
                case IsolationLevel.RepeatableRead:
                    return System.Data.IsolationLevel.RepeatableRead;
                case IsolationLevel.ReadCommitted:
                    return System.Data.IsolationLevel.ReadCommitted;
                case IsolationLevel.ReadUncommitted:
                    return System.Data.IsolationLevel.ReadUncommitted;
                case IsolationLevel.Snapshot:
                    return System.Data.IsolationLevel.Snapshot;
                case IsolationLevel.Chaos:
                    return System.Data.IsolationLevel.Chaos;
                case IsolationLevel.Unspecified:
                    return System.Data.IsolationLevel.Unspecified;
            }

            return System.Data.IsolationLevel.ReadCommitted;
        }

        System.Data.IsolationLevel isolationLevel;
        string connectionString;
    }
}
