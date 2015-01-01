namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class NoTransactionReceiveBehavior : IBehavior<IncomingContext>
    {
        readonly string connectionString;
        readonly TableBasedQueue errorQueue;

        public NoTransactionReceiveBehavior(string connectionString, TableBasedQueue errorQueue)
        {
            this.connectionString = connectionString;
            this.errorQueue = errorQueue;
        }

        public void Invoke(IncomingContext context, Action next)
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

            context.PhysicalMessage = readResult.Message;
            next();
        }

        public class Registration : RegisterStep
        {
            public Registration(string errorQueueAddress)
                : base("ReceiveMessage", typeof(NoTransactionReceiveBehavior), "Performs a SQL receive without a transaction.")
            {
                InsertBeforeIfExists(WellKnownStep.ExecuteLogicalMessages);
                ContainerRegistration((builder, settings) =>
                {
                    var connectionInfo = builder.Build<LocalConnectionParams>();
                    var errorQueue = new TableBasedQueue(errorQueueAddress, connectionInfo.Schema);

                    return new NoTransactionReceiveBehavior(connectionInfo.ConnectionString, errorQueue);
                });
            }
        }
    }
}