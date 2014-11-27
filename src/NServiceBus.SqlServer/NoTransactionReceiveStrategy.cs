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
            MessageReadResult readResult;
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
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