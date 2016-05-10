namespace NServiceBus.Transport.SQLServer
{
    using System.Threading.Tasks;
    using System.Transactions;
    using Transports;
    using IsolationLevel = System.Data.IsolationLevel;

    class ReceiveWithSendsAtomicWithReceiveTransaction : ReceiveStrategy
    {
        public ReceiveWithSendsAtomicWithReceiveTransaction(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;

            isolationLevel = IsolationLevelMapper.Map(transactionOptions.IsolationLevel);
        }

        protected override async Task<ReceiveStrategyContext> CreateContext(TableBasedQueue inputQueue)
        {
            var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false);
            var transaction = connection.BeginTransaction(isolationLevel);
            return new ReceiveStrategyContext(connection, transaction, new ReceiveConnectionDispatchStrategy(connection, transaction));
        }
        
        IsolationLevel isolationLevel;
        SqlConnectionFactory connectionFactory;
    }
}