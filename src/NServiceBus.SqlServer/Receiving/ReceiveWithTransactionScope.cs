namespace NServiceBus.Transport.SQLServer
{
    using System.Threading.Tasks;
    using System.Transactions;
    using Transports;

    class ReceiveWithTransactionScope : ReceiveStrategy
    {
        public ReceiveWithTransactionScope(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
        }

        protected override async Task<ReceiveStrategyContext> CreateContext(TableBasedQueue inputQueue)
        {
            var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
            var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false);

            return new ReceiveStrategyContext(connection, scope, new ReceiveConnectionDispatchStrategy(connection, null));
        }

        TransactionOptions transactionOptions;
        SqlConnectionFactory connectionFactory;
    }
}