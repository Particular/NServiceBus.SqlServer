namespace NServiceBus.Transports.SQLServer
{
    using NServiceBus.Pipeline;

    interface ICallbackAddressStore
    {
        void SetCallbackAddress(Address callbackAddress);
        Address TryGetCallbackAddress();
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

        public Address TryGetCallbackAddress()
        {
            Address callbackAddress;
            behaviorContext.TryGet(SqlServerCallbackAddressContextKey, out callbackAddress);
            return callbackAddress;
        }
    }
}