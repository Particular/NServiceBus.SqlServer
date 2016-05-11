namespace NServiceBus.Transport.SQLServer
{
    using System.Transactions;
    using IsolationLevel = System.Data.IsolationLevel;

    class ReceiveWithReceiveOnlyTransaction : ReceiveStrategy<ReceiveStrategyContextForNativeTransaction>
    {
        public ReceiveWithReceiveOnlyTransaction(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;

            isolationLevel = IsolationLevelMapper.Map(transactionOptions.IsolationLevel);
        }

        protected override ReceiveStrategyContextForNativeTransaction CreateContext(TableBasedQueue inputQueue)
        {
            return new ReceiveStrategyContextForNativeTransaction(() => connectionFactory.OpenNewConnection(), isolationLevel);
        }

        protected override IDispatchStrategy CreateDispatchStrategy(ReceiveStrategyContextForNativeTransaction context)
        {
            return new SeparateConnectionDispatchStrategy(connectionFactory);
        }

        IsolationLevel isolationLevel;
        SqlConnectionFactory connectionFactory;
    }
}