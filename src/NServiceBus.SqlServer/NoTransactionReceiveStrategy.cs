namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;

    class NoTransactionReceiveStrategy : IReceiveStrategy
    {
        readonly string connectionString;
        readonly TableBasedQueue errorQueue;
        readonly Func<TransportMessage, bool> tryProcessMessageCallback;
        readonly Func<string, SqlConnection> sqlConnectionFactory;

        public NoTransactionReceiveStrategy(string connectionString, TableBasedQueue errorQueue, Func<TransportMessage, bool> tryProcessMessageCallback, Func<string, SqlConnection> sqlConnectionFactory)
        {
            this.connectionString = connectionString;
            this.errorQueue = errorQueue;
            this.tryProcessMessageCallback = tryProcessMessageCallback;
            this.sqlConnectionFactory = sqlConnectionFactory;
        }

        public ReceiveResult TryReceiveFrom(TableBasedQueue queue)
        {
            MessageReadResult readResult;
            using (var connection = sqlConnectionFactory(connectionString))
            {
                readResult = queue.TryReceive(connection);
                if (readResult.IsPoison)
                {
                    errorQueue.Send(readResult.DataRecord, connection);
                    return ReceiveResult.NoMessage();
                }
            }

            if (!readResult.Successful)
            {
                return ReceiveResult.NoMessage();
            }

            var result = ReceiveResult.Received(readResult.Message);
            try
            {
                tryProcessMessageCallback(readResult.Message);
                return result;
            }
            catch (Exception ex)
            {
                return result.FailedProcessing(ex);
            }
        }
    }
}