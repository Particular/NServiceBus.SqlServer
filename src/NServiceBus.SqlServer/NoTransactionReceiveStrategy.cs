namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Transactions;
    using NServiceBus.Pipeline;

    class NoTransactionReceiveStrategy : ReceiveStrategyBase, IReceiveStrategy
    {
        public NoTransactionReceiveStrategy(Func<TransportMessage, bool> tryProcessMessage, TransportMessageReader transportMessageReader, string connectionString, PipelineExecutor pipelineExecutor, TransactionOptions transactionOptions) 
            : base(tryProcessMessage, transportMessageReader, connectionString, pipelineExecutor, transactionOptions)
        {
        }

        public ReceiveResult TryReceiveFrom(string tableName)
        {
            var result = new ReceiveResult();

            var message = Receive(GetQueryForTable(tableName));

            if (message == null)
            {
                return result;
            }

            result.Message = message;
            try
            {
                tryProcessMessage(message);
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }

            return result;
        }

        TransportMessage Receive(string sql)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(sql, connection)
                {
                    CommandType = CommandType.Text
                })
                {
                    return transportMessageReader.ExecuteReader(command);
                }
            }
        }
    }
}