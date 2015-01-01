namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Transactions;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class AmbientTransactionReceiveBehavior : IBehavior<IncomingContext>
    {
        readonly string connectionString;
        readonly TableBasedQueue errorQueue;
        readonly TransactionOptions transactionOptions;

        public AmbientTransactionReceiveBehavior(string connectionString, TableBasedQueue errorQueue, TransactionOptions transactionOptions)
        {
            this.errorQueue = errorQueue;
            this.transactionOptions = transactionOptions;
            this.connectionString = connectionString;
        }

        public void Invoke(IncomingContext context, Action next)
        {
            var queue = context.Get<TableBasedQueue>();
            var messageAvailabilitySignaller = context.Get<IMessageAvailabilitySignaller>();

            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (context.SetConnection(connectionString, connection))
                    {
                        var readResult = queue.TryReceive(connection);
                        if (readResult.IsPoison)
                        {
                            errorQueue.Send(readResult.DataRecord, connection);
                            scope.Complete();
                            return;
                        }

                        if (!readResult.Successful)
                        {
                            scope.Complete();
                            return;
                        }

                        messageAvailabilitySignaller.MessageAvailable();
                        context.PhysicalMessage = readResult.Message;
                        next();

                        if (context.MessageHandledSuccessfully())
                        {
                            scope.Complete();
                            scope.Dispose(); // We explicitly calling Dispose so that we force any exception to not bubble, eg Concurrency/Deadlock exception.
                        }
                    }
                }
            }
        }

        public class Registration : RegisterStep
        {
            public Registration(string errorQueueAddress, TransactionOptions transactionOptions)
                : base("ReceiveMessage", typeof(AmbientTransactionReceiveBehavior), "Performs a SQL receive using a transaction scope.")
            {
                InsertBeforeIfExists(WellKnownStep.ExecuteLogicalMessages);
                ContainerRegistration((builder, settings) =>
                {
                    var connectionInfo = builder.Build<LocalConnectionParams>();
                    var errorQueue = new TableBasedQueue(errorQueueAddress, connectionInfo.Schema);

                    return new AmbientTransactionReceiveBehavior(connectionInfo.ConnectionString, errorQueue, transactionOptions);
                });
            }
        }
    }
}