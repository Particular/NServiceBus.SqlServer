public interface IPlugin
{
    Task Start(string[] args, CancellationToken cancellationToken = default);
    Task Stop(CancellationToken cancellationToken = default);
}
