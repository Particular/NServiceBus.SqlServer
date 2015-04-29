namespace NServiceBus.Transports.SQLServer
{
    using NServiceBus.Pipeline;

    static class CallbackQueuePipelineContextExtensions
    {
        const string SqlServerCallbackAddressContextKey = "SqlServerCallbackAddress";

        public static void SetCallbackAddress(this BehaviorContext context, string callbackAddress)
        {
            context.Set(SqlServerCallbackAddressContextKey,callbackAddress);
        }

        public static string TryGetCallbackAddress(this BehaviorContext context)
        {
            string callbackAddress;
            context.TryGet(SqlServerCallbackAddressContextKey, out callbackAddress);
            return callbackAddress;
        }
    }
}