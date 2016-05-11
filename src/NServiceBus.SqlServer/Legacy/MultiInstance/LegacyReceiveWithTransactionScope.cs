namespace NServiceBus.Transport.SQLServer
{
    using System.Threading.Tasks;
    using System.Transactions;

    class LegacyReceiveWithTransactionScope : ReceiveStrategy<ReceiveStrategyContextForAmbientTransaction>
    {
        public LegacyReceiveWithTransactionScope(TransactionOptions transactionOptions, LegacySqlConnectionFactory connectionFactory)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
        }

        protected override ReceiveStrategyContextForAmbientTransaction CreateContext(TableBasedQueue inputQueue)
        {
            return new ReceiveStrategyContextForAmbientTransaction(() => connectionFactory.OpenNewConnection(inputQueue.TransportAddress), transactionOptions);
        }

        protected override IDispatchStrategy CreateDispatchStrategy(ReceiveStrategyContextForAmbientTransaction context)
        {
            return new LegacyIsolatedDispatchStrategy(connectionFactory);
        }

        protected override async Task DeadLetterPoisonMessage(TableBasedQueue errorQueue, IReceiveStrategyContext context, MessageRow poisonMessage)
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