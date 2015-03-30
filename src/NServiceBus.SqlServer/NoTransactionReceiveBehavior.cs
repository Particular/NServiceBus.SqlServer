namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using NServiceBus.Pipeline.Contexts;

    class NoTransactionReceiveBehavior : ReceiveBehavior
    {
        readonly string connectionString;
        readonly TableBasedQueue errorQueue;

        public NoTransactionReceiveBehavior(string connectionString, TableBasedQueue errorQueue)
        {
            this.connectionString = connectionString;
            this.errorQueue = errorQueue;
        }

        protected override void Invoke(IncomingContext context, Action<IncomingMessage> onMessage)
        {
            var queue = context.Get<TableBasedQueue>();
            var messageAvailabilitySignaller = context.Get<IMessageAvailabilitySignaller>();

            MessageReadResult readResult;
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                readResult = queue.TryReceive(connection);
                if (readResult.IsPoison)
                {
                    errorQueue.Send(readResult.DataRecord, connection);
                    return;
                }
            }

            if (!readResult.Successful)
            {
                return;
            }
            messageAvailabilitySignaller.MessageAvailable();

            onMessage(readResult.Message);
        }
    }
}