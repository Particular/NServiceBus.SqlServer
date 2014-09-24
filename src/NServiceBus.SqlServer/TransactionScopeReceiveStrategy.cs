namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Transactions;
    using NServiceBus.Pipeline;

    class TransactionScopeReceiveStrategy : ReceiveStrategyBase, IReceiveStrategy
    {
        public TransactionScopeReceiveStrategy(Func<TransportMessage, bool> tryProcessMessage, TransportMessageReader transportMessageReader, string connectionString, PipelineExecutor pipelineExecutor, TransactionOptions transactionOptions) 
            : base(tryProcessMessage, transportMessageReader, connectionString, pipelineExecutor, transactionOptions)
        {
            pipelineContext = string.Format("SqlConnection-{0}", connectionString);
        }

        public ReceiveResult TryReceiveFrom(string tableName)
        {
            var result = new ReceiveResult();

            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    try
                    {
                        pipelineExecutor.CurrentContext.Set(pipelineContext, connection);

                        connection.Open();

                        TransportMessage message;

                        using (var command = new SqlCommand(GetQueryForTable(tableName), connection)
                        {
                            CommandType = CommandType.Text
                        })
                        {
                            message = transportMessageReader.ExecuteReader(command);
                        }

                        if (message == null)
                        {
                            scope.Complete();
                            return result;
                        }

                        result.Message = message;

                        try
                        {
                            if (tryProcessMessage(message))
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
                    finally
                    {
                        pipelineExecutor.CurrentContext.Remove(pipelineContext);
                    }
                }
            }
        }

        string pipelineContext;
    }
}