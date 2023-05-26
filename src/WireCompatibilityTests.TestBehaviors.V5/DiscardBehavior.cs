using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Pipeline;

public class DiscardBehavior : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
{
    readonly string testRunId;

    public DiscardBehavior(string testRunId)
    {
        this.testRunId = testRunId;
    }

    public Task Invoke(IIncomingPhysicalMessageContext context, Func<IIncomingPhysicalMessageContext, Task> next)
    {
        if (context.MessageHeaders.TryGetValue(Headers.MessageIntent, out var intent) && intent == "Subscribe")
        {
            //Subscribe messages don't get stamped with test run it
            return next(context);
        }

        if (!context.MessageHeaders.TryGetValue("TestRunId", out var testRunId) || testRunId != this.testRunId)
        {
            return Task.CompletedTask;
        }

        return next(context);
    }
}
