namespace NServiceBus.Transport.SQLServer
{
    class TableBasedQueueDispatcher : IQueueDispatcher
    {
        IsolatedDispatchSendStrategy isolatedDispatchStrategy;
        SeparateConnectionDispatchStrategy createNonIsolatedDispatchStrategy;

        public TableBasedQueueDispatcher(SqlConnectionFactory connectionFactory)
        {
            isolatedDispatchStrategy = new IsolatedDispatchSendStrategy(connectionFactory);
            createNonIsolatedDispatchStrategy = new SeparateConnectionDispatchStrategy(connectionFactory);
        }

        public IDispatchStrategy CreateIsolatedDispatchStrategy()
        {
            return isolatedDispatchStrategy;
        }

        public IDispatchStrategy CreateNonIsolatedDispatchStrategy()
        {
            return createNonIsolatedDispatchStrategy;
        }
    }
}