namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;

    class NoTransactionReceiveStrategy : IReceiveStrategy
    {
        readonly string connectionString;
        readonly TableBasedQueue errorQueue;
        readonly Func<TransportMessage, bool> tryProcessMessageCallback;

        public NoTransactionReceiveStrategy(string connectionString, TableBasedQueue errorQueue, Func<TransportMessage, bool> tryProcessMessageCallback)
        {
            this.connectionString = connectionString;
            this.errorQueue = errorQueue;
            this.tryProcessMessageCallback = tryProcessMessageCallback;
        }

        public ReceiveResult TryReceiveFrom(TableBasedQueue queue)
        {
            var result = new ReceiveResult();
            MessageReadResult readResult;
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                readResult = queue.TryReceive(connection);
                if (readResult.IsPoison)
                {
                    errorQueue.Send(readResult.DataRecord, connection);
                    return result;
                }
            }

            if (!readResult.Successful)
            {
                return result;
            }

            result.Message = readResult.Message;
            try
            {
                tryProcessMessageCallback(readResult.Message);
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }

            return result;
        }
    }
}