namespace NServiceBus.Transports.SQLServer
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;


    class ReadCallbackAddressBehavior : IBehavior<OutgoingContext>
    {
        public void Invoke(OutgoingContext context, Action next)
        {
            string callbackQueue;

            if (context.IncomingMessage != null && context.IncomingMessage.Headers.TryGetValue(SqlServerMessageSender.CallbackHeaderKey, out callbackQueue))
            {
                context.SetCallbackAddress(Address.Parse(callbackQueue));
            }
            next();
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("ReadCallbackAddressBehavior", typeof(ReadCallbackAddressBehavior), "Reads the NServiceBus.SqlServer.CallbackQueue header and stores it in the outgoing context")
            {
                InsertAfter(WellKnownStep.SerializeMessage);
                InsertBefore(WellKnownStep.DispatchMessageToTransport);
            }
        }
    }
}