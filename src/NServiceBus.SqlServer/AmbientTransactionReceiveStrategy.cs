namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Transactions;
    using NServiceBus.Unicast.Transport;

    class AmbientTransactionReceiveStrategy : IReceiveStrategy
    {
        readonly string connectionString;
        readonly TableBasedQueue errorQueue;
        readonly Func<TransportMessage, bool> tryProcessMessageCallback;
        readonly TransactionOptions transactionOptions;
        readonly ConnectionFactory sqlConnectionFactory;
        readonly IConnectionStore connectionStore;

        public AmbientTransactionReceiveStrategy(string connectionString, TableBasedQueue errorQueue, Func<TransportMessage, bool> tryProcessMessageCallback, ConnectionFactory sqlConnectionFactory, IConnectionStore connectionStore, TransactionSettings transactionSettings)
        {
            this.tryProcessMessageCallback = tryProcessMessageCallback;
            this.errorQueue = errorQueue;
            this.connectionString = connectionString;
            this.sqlConnectionFactory = sqlConnectionFactory;
            this.connectionStore = connectionStore;

            transactionOptions = new TransactionOptions
            {
                IsolationLevel = transactionSettings.IsolationLevel,
                Timeout = transactionSettings.TransactionTimeout
            };
        }

        public ReceiveResult TryReceiveFrom(TableBasedQueue queue)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
            {
                using (var connection = sqlConnectionFactory.OpenNewConnection(connectionString))
                {
                    using (connectionStore.SetConnection(connectionString, connection))
                    {
                        var readResult = queue.TryReceive(connection);
                        if (readResult.IsPoison)
                        {
                            errorQueue.Send(readResult.DataRecord, connection);
                            scope.Complete();
                            return ReceiveResult.NoMessage();
                        }

                        if (!readResult.Successful)
                        {
                            scope.Complete();
                            return ReceiveResult.NoMessage();
                        }

                        var result = ReceiveResult.Received(readResult.Message);

                        try
                        {
                            if (tryProcessMessageCallback(readResult.Message))
                            {
                                scope.Complete();
                                scope.Dispose(); // We explicitly calling Dispose so that we force any exception to not bubble, eg Concurrency/Deadlock exception.
                            }
                            return result;
                        }
                        catch (Exception ex)
                        {
                            return result.FailedProcessing(ex);
                        }
                    }
                }
            }
        }
    }
}