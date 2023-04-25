using System.Threading;
using System.Threading.Tasks;
using TestAgent.Framework;

public class Plugin : IPlugin
{
    public async Task ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        await TestAgentFacade.Run(args, cancellationToken).ConfigureAwait(false);
    }
}
