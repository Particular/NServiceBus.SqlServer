namespace NServiceBus.Transports.SQLServer
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Transports.SQLServer.Config;

    class ReadIncomingCallbackAddressBehavior : LogicalMessageProcessingStageBehavior
    {
        public override void Invoke(Context context, Action next)
        {
            string incomingCallbackQueue;
            if (context.IncomingLogicalMessage != null && context.IncomingLogicalMessage.Headers.TryGetValue(CallbackConfig.CallbackHeaderKey, out incomingCallbackQueue))
            {
                context.SetCallbackAddress(incomingCallbackQueue);
            }
            next();
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("ReadIncomingCallbackAddressBehavior", typeof(ReadIncomingCallbackAddressBehavior), "Reads the callback address specified by the message sender and puts it into the context.")
            {
            }
        }
    }
}