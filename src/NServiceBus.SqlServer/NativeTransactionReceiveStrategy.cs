namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using NServiceBus.Pipeline;
    using NServiceBus.Unicast.Transport;

    class NativeTransactionReceiveStrategy : IReceiveStrategy
    {
        readonly PipelineExecutor pipelineExecutor;
        readonly string connectionString;
        readonly TableBasedQueue errorQueue;
        readonly Func<TransportMessage, bool> tryProcessMessageCallback;
        readonly IsolationLevel isolationLevel;
        readonly ConnectionFactory sqlConnectionFactory;

        public NativeTransactionReceiveStrategy(string connectionString, TableBasedQueue errorQueue, Func<TransportMessage, bool> tryProcessMessageCallback, ConnectionFactory sqlConnectionFactory, PipelineExecutor pipelineExecutor, TransactionSettings transactionSettings)
        {
            this.pipelineExecutor = pipelineExecutor;
            this.tryProcessMessageCallback = tryProcessMessageCallback;
            this.errorQueue = errorQueue;
            this.connectionString = connectionString;
            this.sqlConnectionFactory = sqlConnectionFactory;
            isolationLevel = GetSqlIsolationLevel(transactionSettings.IsolationLevel);
        }

        public ReceiveResult TryReceiveFrom(TableBasedQueue queue)
        {
            using (var connection = sqlConnectionFactory.OpenNewConnection(connectionString))
            {
                using (pipelineExecutor.SetConnection(connectionString, connection))
                {
                    using (var transaction = connection.BeginTransaction(isolationLevel))
                    {
                        using (pipelineExecutor.SetTransaction(connectionString, transaction))
                        {
                            MessageReadResult readResult;
                            try
                            {
                                readResult = queue.TryReceive(connection, transaction);
                            }
                            catch (Exception)
                            {
                                transaction.Rollback();
                                throw;
                            }

                            if (readResult.IsPoison)
                            {
                                errorQueue.Send(readResult.DataRecord, connection, transaction);
                                transaction.Commit();
                                return ReceiveResult.NoMessage();
                            }

                            if (!readResult.Successful)
                            {
                                transaction.Commit();
                                return ReceiveResult.NoMessage();
                            }

                            var result = ReceiveResult.Received(readResult.Message);
                            try
                            {
                                if (tryProcessMessageCallback(result.Message))
                                {
                                    transaction.Commit();
                                }
                                else
                                {
                                    transaction.Rollback();
                                }
                                return result;
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();
                                return result.FailedProcessing(ex);
                            }
                        }
                    }
                }
            }
        }


        static IsolationLevel GetSqlIsolationLevel(System.Transactions.IsolationLevel isolationLevel)
        {
            switch (isolationLevel)
            {
                case System.Transactions.IsolationLevel.Serializable:
                    return IsolationLevel.Serializable;
                case System.Transactions.IsolationLevel.RepeatableRead:
                    return IsolationLevel.RepeatableRead;
                case System.Transactions.IsolationLevel.ReadCommitted:
                    return IsolationLevel.ReadCommitted;
                case System.Transactions.IsolationLevel.ReadUncommitted:
                    return IsolationLevel.ReadUncommitted;
                case System.Transactions.IsolationLevel.Snapshot:
                    return IsolationLevel.Snapshot;
                case System.Transactions.IsolationLevel.Chaos:
                    return IsolationLevel.Chaos;
                case System.Transactions.IsolationLevel.Unspecified:
                    return IsolationLevel.Unspecified;
            }

            return IsolationLevel.ReadCommitted;
        }
    }
}