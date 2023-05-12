namespace TestAgent.Framework;

using System;
using System.Threading.Tasks;
using NServiceBus.Pipeline;
using NServiceBus.Transport;

public class StampVersionBehavior : Behavior<IOutgoingPhysicalMessageContext>
{
    string versionString;

    public StampVersionBehavior(IDispatchMessages dispatcher)
    {
        var fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(dispatcher.GetType().Assembly.Location);
        versionString = fileVersionInfo.ProductVersion;
    }

    public override Task Invoke(IOutgoingPhysicalMessageContext context, Func<Task> next)
    {
        context.Headers["WireCompatVersion"] = versionString;
        return next();
    }
}