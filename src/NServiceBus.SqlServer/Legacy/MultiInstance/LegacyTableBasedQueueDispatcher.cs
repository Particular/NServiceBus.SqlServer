namespace NServiceBus.Transport.SQLServer
{
    class LegacyTableBasedQueueDispatcher : IQueueDispatcher
    {
        public LegacyTableBasedQueueDispatcher(LegacySqlConnectionFactory connectionFactory)
        {
            createIsolatedDispatchStrategy = new LegacyIsolatedDispatchStrategy(connectionFactory);
            createNonIsolatedDispatchStrategy = new LegacyNonIsolatedDispatchStrategy(connectionFactory);
        }

        public IDispatchStrategy CreateIsolatedDispatchStrategy()
        {
            return createIsolatedDispatchStrategy;
        }

        public IDispatchStrategy CreateNonIsolatedDispatchStrategy()
        {
            return createNonIsolatedDispatchStrategy;
        }

        LegacyIsolatedDispatchStrategy createIsolatedDispatchStrategy;
        LegacyNonIsolatedDispatchStrategy createNonIsolatedDispatchStrategy;
    }
}