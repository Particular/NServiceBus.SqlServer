namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Pipeline;

    class CompatibilityModeSubscriptionValidationBehavior : Behavior<ISubscribeContext>
    {
        NativelySubscribedEvents nativeOnlySubscribeTypes;

        public CompatibilityModeSubscriptionValidationBehavior(NativelySubscribedEvents nativeOnlySubscribeTypes)
        {
            this.nativeOnlySubscribeTypes = nativeOnlySubscribeTypes;
        }

        public override async Task Invoke(ISubscribeContext context, Func<Task> next)
        {
            await next().ConfigureAwait(false);

            var subscribeResult = context.Extensions.Get<SubscribeResult>();
            if (subscribeResult.InvokedNativelyOnly.Contains(context.EventType.AssemblyQualifiedName)
                && !nativeOnlySubscribeTypes.IsNativelySubscribed(context.EventType))
            {
                throw new Exception("When an endpoint is set to message-driven pub/sub compatibility mode, all subscribed events need to be configured "
                                    + $"in the compatibility settings through either RegisterPublisher or SubscribeNatively. Event {context.EventType.FullName} is not configured. "
                                    + "If that event is published by a message-driven pub/sub endpoint, use RegisterPublisher. Otherwise use SubscribeNatively.");
            }
        }
    }
}