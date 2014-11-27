namespace NServiceBus.Transports.SQLServer
{
    using NServiceBus.Pipeline;

    static class CallbackQueuePipelineContextExtensions
    {
        const string SqlServerCallbackAddressContextKey = "SqlServerCallbackAddress";

        public static void SetCallbackAddress(this BehaviorContext context, Address callbackAddress)
        {
            context.Set(SqlServerCallbackAddressContextKey,callbackAddress);
        }

        public static Address TryGetCallbackAddress(this BehaviorContext context)
        {
            Address callbackAddress;
            context.TryGet(SqlServerCallbackAddressContextKey, out callbackAddress);
            return callbackAddress;
        }
    }
}