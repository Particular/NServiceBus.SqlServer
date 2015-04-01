namespace NServiceBus.Transports.SQLServer
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class SetOutgoingCallbackAddressBehavior : PhysicalOutgoingContextStageBehavior
    {
        readonly OutgoingCallbackAddressSetter callbackAddressSetter;

        public SetOutgoingCallbackAddressBehavior(OutgoingCallbackAddressSetter callbackAddressSetter)
        {
            this.callbackAddressSetter = callbackAddressSetter;
        }

        public override void Invoke(Context context, Action next)
        {
            callbackAddressSetter.SetCallbackAddress(context.OutgoingMessage);
            next();
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("SetOutgoingCallbackAddressBehavior", typeof(SetOutgoingCallbackAddressBehavior), "Writes out callback address to in outgoing message.")
            {
                InsertAfter(WellKnownStep.SerializeMessage);
                InsertBefore(WellKnownStep.DispatchMessageToTransport);
            }
        }
    }
}