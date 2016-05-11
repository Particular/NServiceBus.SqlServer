namespace NServiceBus.Transport.SQLServer
{
    using System;
    using Transports;

    class LegacyDispatchPolicy : IDispatchPolicy
    {
        public LegacyDispatchPolicy(LegacySqlConnectionFactory connectionFactory)
        {
            isolatedDispatchStrategy = new LegacyIsolatedDispatchStrategy(connectionFactory);
            nonIsolatedDispatchStrategy = new LegacyNonIsolatedDispatchStrategy(connectionFactory);
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

        LegacyIsolatedDispatchStrategy isolatedDispatchStrategy;
        LegacyNonIsolatedDispatchStrategy nonIsolatedDispatchStrategy;
        
    }
}