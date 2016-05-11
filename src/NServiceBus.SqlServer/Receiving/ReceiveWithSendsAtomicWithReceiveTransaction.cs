namespace NServiceBus.Transport.SQLServer
{
    using System.Transactions;
    using IsolationLevel = System.Data.IsolationLevel;

    class ReceiveWithSendsAtomicWithReceiveTransaction : ReceiveStrategy<ReceiveStrategyContextForNativeTransaction>
    {
        public ReceiveWithSendsAtomicWithReceiveTransaction(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory)
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
            return new ReceiveConnectionDispatchStrategy(context.Connection, context.Transaction);
        }

        IsolationLevel isolationLevel;
        SqlConnectionFactory connectionFactory;
    }
}