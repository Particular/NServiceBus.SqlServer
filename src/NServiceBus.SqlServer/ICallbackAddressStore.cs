namespace NServiceBus.Transports.SQLServer
{
    using NServiceBus.Pipeline;

    interface ICallbackAddressStore
    {
        void SetCallbackAddress(Address callbackAddress);
        bool TryGetCallbackAddress(out Address callbackAddress);
    }

    class ContextualCallbackAddressStore : ICallbackAddressStore
    {
        readonly BehaviorContext behaviorContext;
        const string SqlServerCallbackAddressContextKey = "SqlServerCallbackAddress";

        public ContextualCallbackAddressStore(BehaviorContext behaviorContext)
        {
            this.behaviorContext = behaviorContext;
        }

        public void SetCallbackAddress(Address callbackAddress)
        {
            behaviorContext.Set(SqlServerCallbackAddressContextKey,callbackAddress);
        }

        public bool TryGetCallbackAddress(out Address callbackAddress)
        {
            return behaviorContext.TryGet(SqlServerCallbackAddressContextKey, out callbackAddress);
        }
    }
}