namespace NServiceBus.Transport.SQLServer
{
    class ReceiveWithNoTransaction : ReceiveStrategy<ReceiveStrategyContextForNoTransaction>
    {
        public ReceiveWithNoTransaction(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        protected override ReceiveStrategyContextForNoTransaction CreateContext(TableBasedQueue inputQueue)
        {
            return new ReceiveStrategyContextForNoTransaction(() => connectionFactory.OpenNewConnection());
        }

        protected override IDispatchStrategy CreateDispatchStrategy(ReceiveStrategyContextForNoTransaction context)
        {
            return new SeparateConnectionDispatchStrategy(connectionFactory);
        }

        SqlConnectionFactory connectionFactory;
    }
}