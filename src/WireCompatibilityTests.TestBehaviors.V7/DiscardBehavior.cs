using System;
using System.Threading.Tasks;
using NServiceBus.Pipeline;

public class DiscardBehavior : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
{
    readonly string TestRunId;

    public DiscardBehavior(string testRunId)
    {
        TestRunId = testRunId;
    }

    public Task Invoke(IIncomingPhysicalMessageContext context, Func<IIncomingPhysicalMessageContext, Task> next)
    {
        if (!context.MessageHeaders.TryGetValue("TestRunId", out var testRunId) || testRunId != TestRunId)
        {
            return Task.CompletedTask;
        }

        return next(context);
    }
}
