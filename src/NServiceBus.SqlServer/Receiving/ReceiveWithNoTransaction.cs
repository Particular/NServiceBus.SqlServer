namespace NServiceBus.Transport.SQLServer
{
    using System.Threading.Tasks;


    class ReceiveWithNoTransaction : ReceiveStrategy
    {
        public ReceiveWithNoTransaction(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        protected override async Task<ReceiveStrategyContext> CreateContext(TableBasedQueue inputQueue)
        {
            var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false);
            return new ReceiveStrategyContext(connection, new SeparateConnectionDispatchStrategy(connectionFactory));
        }

        SqlConnectionFactory connectionFactory;
    }
}