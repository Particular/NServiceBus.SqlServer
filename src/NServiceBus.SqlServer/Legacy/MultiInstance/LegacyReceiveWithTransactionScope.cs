namespace NServiceBus.Transport.SQLServer
{
    using System.Threading.Tasks;
    using System.Transactions;
    using Transports;

    class LegacyReceiveWithTransactionScope : ReceiveStrategy
    {
        public LegacyReceiveWithTransactionScope(TransactionOptions transactionOptions, LegacySqlConnectionFactory connectionFactory)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
        }

        protected override async Task<ReceiveStrategyContext> CreateContext(TableBasedQueue inputQueue)
        {
            var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
            var connection = await connectionFactory.OpenNewConnection(inputQueue.TransportAddress).ConfigureAwait(false);

            using (var inputConnection = await connectionFactory.OpenNewConnection(inputQueue.TransportAddress).ConfigureAwait(false))

            return new ReceiveStrategyContext(connection, scope, new LegacyNonIsolatedDispatchStrategy(connectionFactory));
        }

        protected override async Task DeadLetterPoisonMessage(TableBasedQueue errorQueue, ReceiveStrategyContext context, MessageRow poisonMessage)
        {
            using (var errorConnection = await connectionFactory.OpenNewConnection(errorQueue.TransportAddress).ConfigureAwait(false))
            {
                await errorQueue.DeadLetter(poisonMessage, errorConnection, null).ConfigureAwait(false);
            }
        }

        TransactionOptions transactionOptions;
        LegacySqlConnectionFactory connectionFactory;
    }
}