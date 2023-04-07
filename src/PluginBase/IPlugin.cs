public interface IPlugin
{
    Task ExecuteAsync(string[] args, CancellationToken cancellationToken = default);
}
