namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Transactions;
    using NServiceBus.Pipeline;
    using IsolationLevel = System.Data.IsolationLevel;

    class NativeTransactionReceiveStrategy : ReceiveStrategyBase, IReceiveStrategy
    {
        
        public NativeTransactionReceiveStrategy(Func<TransportMessage, bool> tryProcessMessage, TransportMessageReader transportMessageReader, string connectionString, PipelineExecutor pipelineExecutor, TransactionOptions transactionOptions) 
            : base(tryProcessMessage, transportMessageReader, connectionString, pipelineExecutor, transactionOptions)
        {
            pipelineConnectionContext = string.Format("SqlConnection-{0}", base.connectionString);
            pipelineTransactionContext = string.Format("SqlTransaction-{0}", base.connectionString);
        }

        public ReceiveResult TryReceiveFrom(string tableName)
        {
            var result = new ReceiveResult();

            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    pipelineExecutor.CurrentContext.Set(pipelineConnectionContext, connection);

                    connection.Open();

                    using (var transaction = connection.BeginTransaction(GetSqlIsolationLevel(transactionOptions.IsolationLevel)))
                    {
                        try
                        {
                            pipelineExecutor.CurrentContext.Set(pipelineTransactionContext, transaction);

                            TransportMessage message;
                            try
                            {
                                message = ReceiveWithNativeTransaction(GetQueryForTable(tableName), connection, transaction);
                            }
                            catch (Exception)
                            {
                                transaction.Rollback();
                                throw;
                            }

                            if (message == null)
                            {
                                transaction.Commit();
                                return result;
                            }

                            result.Message = message;

                            try
                            {
                                if (tryProcessMessage(message))
                                {
                                    transaction.Commit();
                                }
                                else
                                {
                                    transaction.Rollback();
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Exception = ex;
                                transaction.Rollback();
                            }

                            return result;
                        }
                        finally
                        {
                            pipelineExecutor.CurrentContext.Remove(pipelineTransactionContext);
                        }
                    }
                }
                finally
                {
                    pipelineExecutor.CurrentContext.Remove(pipelineConnectionContext);
                }
            }
        }

        TransportMessage ReceiveWithNativeTransaction(string sql, SqlConnection connection, SqlTransaction transaction)
        {
            using (var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                return transportMessageReader.ExecuteReader(command);
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

        readonly string pipelineConnectionContext;
        readonly string pipelineTransactionContext;
    }
}