namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Pipeline;

    class ReadIncomingLegacyCallbackAddressBehavior : Behavior<IIncomingLogicalMessageContext>
    {
        public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
        {
            string incomingCallbackQueue;
            if (context.Message != null && context.Headers.TryGetValue("NServiceBus.SqlServer.CallbackQueue", out incomingCallbackQueue))
            {
                context.Extensions.Set(new CallbackAddress(incomingCallbackQueue));
            }

            await next().ConfigureAwait(false);
        }
    }
}