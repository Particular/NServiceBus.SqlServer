namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Janitor;
    using Transports;

    [SkipWeaving]
    class ReceiveStrategyContextForNoTransaction : IReceiveStrategyContext
    {
        public ReceiveStrategyContextForNoTransaction(Func<Task<SqlConnection>> connectionFactory)
        {
            this.connectionFactory = connectionFactory;
            TransportTransaction = new TransportTransaction();
        }

        public void Dispose()
        {
            connection?.Dispose();
        }

        public async Task Initialize()
        {
            connection = await connectionFactory().ConfigureAwait(false);
            TransportTransaction.Set(connection);
        }

        public Task<MessageReadResult> TryReceive(TableBasedQueue queue)
        {
            return queue.TryReceive(connection, null);
        }

        public Task DeadLetter(TableBasedQueue queue, MessageRow poisonMessage)
        {
            return queue.DeadLetter(poisonMessage, connection, null);
        }

        public TransportTransaction TransportTransaction { get; }
        
        public void Rollback()
        {
            //NOOP
        }

        public void Commit()
        {
            //NOOP
        }

        Func<Task<SqlConnection>> connectionFactory;
        SqlConnection connection;
    }
}