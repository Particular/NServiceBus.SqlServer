namespace NServiceBus.Transports.SQLServer
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class PromoteCallbackQueueBehavior : IBehavior<OutgoingContext>
    {
        public void Invoke(OutgoingContext context, Action next)
        {
            string callbackQueue;

            if (context.IncomingMessage != null && context.IncomingMessage.Headers.TryGetValue(SqlServerMessageSender.CallbackHeaderKey, out callbackQueue))
            {
                context.OutgoingMessage.Headers[SqlServerMessageSender.CallbackHeaderKey] = callbackQueue;
            }

            next();
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("PromoteCallbackQueueBehavior", typeof(PromoteCallbackQueueBehavior), "Propagates the NServiceBus.RabbitMQ.CallbackQueue header to outgoing messages")
            {
                InsertAfter(WellKnownStep.SerializeMessage);
                InsertBefore(WellKnownStep.DispatchMessageToTransport);
            }
        }
    }
}