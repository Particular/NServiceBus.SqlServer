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

#pragma warning disable PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
    public Task Invoke(IIncomingPhysicalMessageContext context, Func<IIncomingPhysicalMessageContext, Task> next)
#pragma warning restore PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
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
