namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Extensibility;
    using Pipeline;
    using Routing;
    using Transports;

    class OverrideOutgoingReplyAddressBehaviorBasedOnLegacyHeader : Behavior<IRoutingContext>
    {
        public override Task Invoke(IRoutingContext context, Func<Task> next)
        {
            string legacyCallbackAddress;
            if (TryGetLegacyCallbackAddress(context, out legacyCallbackAddress)
                && !IsAuditMessage(context)
                && IsReplyMessage(context))
            {
                context.RoutingStrategies = new[]
                {
                    new UnicastRoutingStrategy(legacyCallbackAddress)
                };
            }
            return next();
        }

        static bool IsReplyMessage(IRoutingContext context)
        {
            string messageIntent;
            var headers = context.Message.Headers;
            return headers.TryGetValue(Headers.MessageIntent, out messageIntent) &&
                   messageIntent == MessageIntentEnum.Reply.ToString();
        }

        static bool IsAuditMessage(IRoutingContext context)
        {
            return context.Message.Headers.ContainsKey(Headers.ProcessingEndpoint);
        }

        static bool TryGetLegacyCallbackAddress(IExtendable context, out string callbackAddress)
        {
            callbackAddress = null;
            IncomingMessage incomingMessage;
            return context.Extensions.TryGet(out incomingMessage)
                   && incomingMessage.Headers.TryGetValue("NServiceBus.SqlServer.CallbackQueue", out callbackAddress);
        }
    }
}