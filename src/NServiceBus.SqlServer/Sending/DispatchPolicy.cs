namespace NServiceBus.Transports.SQLServer
{
    using System;

    class DispatchPolicy : IDispatchPolicy
    {
        IsolatedDispatchSendStrategy isolatedDispatchStrategy;
        SeparateConnectionDispatchStrategy nonIsolatedDispatchStrategy;

        public DispatchPolicy(SqlConnectionFactory connectionFactory)
        {
            isolatedDispatchStrategy = new IsolatedDispatchSendStrategy(connectionFactory);
            nonIsolatedDispatchStrategy = new SeparateConnectionDispatchStrategy(connectionFactory);
        }
        
        public IDispatchStrategy CreateDispatchStrategy(DispatchConsistency dispatchConsistency)
        {
            switch (dispatchConsistency)
            {
                case DispatchConsistency.Default:
                    return nonIsolatedDispatchStrategy;
                case DispatchConsistency.Isolated:
                    return isolatedDispatchStrategy;
                default:
                    throw new NotSupportedException("Not supported dispatch consistency: " + dispatchConsistency);
            }
        }
    }
}