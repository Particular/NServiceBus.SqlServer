using System.Threading;
using System.Threading.Tasks;

public interface IPlugin
{
    Task ExecuteAsync(string[] args, CancellationToken cancellationToken = default);
}
