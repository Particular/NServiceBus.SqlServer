namespace NServiceBus.Transport.SQLServer
{
    using System.Transactions;
    using Transports;

    class ReceiveWithTransactionScope : ReceiveStrategy<ReceiveStrategyContextForAmbientTransaction>
    {
        public ReceiveWithTransactionScope(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
        }

        protected override ReceiveStrategyContextForAmbientTransaction CreateContext(TableBasedQueue inputQueue)
        {
            return new ReceiveStrategyContextForAmbientTransaction(() => connectionFactory.OpenNewConnection(), transactionOptions);
        }

        protected override IDispatchStrategy CreateDispatchStrategy(ReceiveStrategyContextForAmbientTransaction context)
        {
            return new ReceiveConnectionDispatchStrategy(context.Connection, null);
        }

        TransactionOptions transactionOptions;
        SqlConnectionFactory connectionFactory;
    }
}