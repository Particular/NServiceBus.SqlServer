namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using Janitor;

    [SkipWeaving]
    class ReceiveStrategyContextForAmbientTransaction : IReceiveStrategyContext
    {
        public SqlConnection Connection { get; private set; }

        public ReceiveStrategyContextForAmbientTransaction(Func<Task<SqlConnection>> connectionFactory, TransactionOptions transactionOptions)
        {
            this.connectionFactory = connectionFactory;
            TransportTransaction = new TransportTransaction();
            ambientTransaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
            TransportTransaction.Set(Transaction.Current);
        }

        public void Dispose()
        {
            Connection?.Dispose();
            ambientTransaction.Dispose();
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
            ambientTransaction.Complete();
        }

        Func<Task<SqlConnection>> connectionFactory;
        TransactionScope ambientTransaction;
    }
}