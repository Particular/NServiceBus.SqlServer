namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Janitor;

    [SkipWeaving]
    class ReceiveStrategyContextForNoTransaction : IReceiveStrategyContext
    {
        public SqlConnection Connection { get; private set; }

        public ReceiveStrategyContextForNoTransaction(Func<Task<SqlConnection>> connectionFactory)
        {
            this.connectionFactory = connectionFactory;
            TransportTransaction = new TransportTransaction();
        }

        public void Dispose()
        {
            Connection?.Dispose();
        }

        public async Task Initialize()
        {
            Connection = await connectionFactory().ConfigureAwait(false);
            TransportTransaction.Set(Connection);
        }

        public Task<MessageReadResult> TryReceive(TableBasedQueue queue)
        {
            return queue.TryReceive(Connection, null);
        }

        public Task DeadLetter(TableBasedQueue queue, MessageRow poisonMessage)
        {
            return queue.DeadLetter(poisonMessage, Connection, null);
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
    }
}