using System.Threading;
using System.Threading.Tasks;
using TestAgent.Framework;

public class Plugin : IPlugin
{
    public Task ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        return TestAgentFacade.Run(args, cancellationToken);
    }
}
