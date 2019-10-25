namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;

    class SubscriptionManager : IManageSubscriptions
    {
        public Task Subscribe(Type eventType, ContextBag context)
        {
            // TODO: Add subscription to table
            throw new NotImplementedException();
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            // TODO: Remove subscription from table
            throw new NotImplementedException();
        }
    }
}