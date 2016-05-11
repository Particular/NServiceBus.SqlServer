namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Janitor;

    [SkipWeaving]
    class ReceiveStrategyContextForNativeTransaction : IReceiveStrategyContext
    {
        public SqlConnection Connection { get; private set; }
        public SqlTransaction Transaction { get; private set; }

        public ReceiveStrategyContextForNativeTransaction(Func<Task<SqlConnection>> connectionFactory, IsolationLevel isolationLevel)
        {
            this.connectionFactory = connectionFactory;
            this.isolationLevel = isolationLevel;
            TransportTransaction = new TransportTransaction();
        }

        public void Dispose()
        {
            Transaction?.Dispose();
            Connection?.Dispose();
        }

        public async Task Initialize()
        {
            Connection = await connectionFactory().ConfigureAwait(false);
            Transaction = Connection.BeginTransaction(isolationLevel);
            TransportTransaction.Set(Connection);
            TransportTransaction.Set(Transaction);
        }

        public Task<MessageReadResult> TryReceive(TableBasedQueue queue)
        {
            return queue.TryReceive(Connection, Transaction);
        }

        public Task DeadLetter(TableBasedQueue queue, MessageRow poisonMessage)
        {
            return queue.DeadLetter(poisonMessage, Connection, Transaction);
        }

        public TransportTransaction TransportTransaction { get; }

        public void Rollback()
        {
            Transaction.Rollback();
        }

        public void Commit()
        {
            Transaction.Commit();
        }

        Func<Task<SqlConnection>> connectionFactory;
        IsolationLevel isolationLevel;
    }
}