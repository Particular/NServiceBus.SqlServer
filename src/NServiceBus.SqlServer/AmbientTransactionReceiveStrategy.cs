namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Transactions;
    using NServiceBus.Pipeline;
    using NServiceBus.Unicast.Transport;

    class AmbientTransactionReceiveStrategy : IReceiveStrategy
    {
        readonly PipelineExecutor pipelineExecutor;
        readonly string connectionString;
        readonly TableBasedQueue errorQueue;
        readonly Func<TransportMessage, bool> tryProcessMessageCallback;
        readonly TransactionOptions transactionOptions;

        public AmbientTransactionReceiveStrategy(string connectionString, TableBasedQueue errorQueue, Func<TransportMessage, bool> tryProcessMessageCallback, PipelineExecutor pipelineExecutor, TransactionSettings transactionSettings)
        {
            this.pipelineExecutor = pipelineExecutor;
            this.tryProcessMessageCallback = tryProcessMessageCallback;
            this.errorQueue = errorQueue;
            this.connectionString = connectionString;
            transactionOptions = new TransactionOptions
            {
                IsolationLevel = transactionSettings.IsolationLevel,
                Timeout = transactionSettings.TransactionTimeout
            };
        }

        public ReceiveResult TryReceiveFrom(TableBasedQueue queue)
        {
            var result = new ReceiveResult();

            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (pipelineExecutor.SetConnection(connectionString, connection))
                    {
                        var readResult = queue.TryReceive(connection);
                        if (readResult.IsPoison)
                        {
                            errorQueue.Send(readResult.DataRecord, connection);
                            scope.Complete();
                            return result;
                        }

                        if (!readResult.Successful)
                        {
                            scope.Complete();
                            return result;
                        }

                        result.Message = readResult.Message;

                        try
                        {
                            if (tryProcessMessageCallback(readResult.Message))
                            {
                                scope.Complete();
                                scope.Dispose(); // We explicitly calling Dispose so that we force any exception to not bubble, eg Concurrency/Deadlock exception.
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Exception = ex;
                        }

                        return result;
                    }
                }
            }
        }
    }
}