namespace NServiceBus.Transports.SQLServer
{
    using System;
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
            string messageIntent;
            if (TryGetLegacyCallbackAddress(context, out legacyCallbackAddress)
                && context.Message.Headers.TryGetValue(Headers.MessageIntent, out messageIntent)
                && messageIntent == MessageIntentEnum.Reply.ToString())
            {
                context.RoutingStrategies = new[]
                {
                    new UnicastRoutingStrategy(legacyCallbackAddress)
                };
            }
            return next();
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