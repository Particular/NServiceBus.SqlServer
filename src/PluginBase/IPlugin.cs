public interface IPlugin
{
    Task StartEndpoint(
        string behaviorName,
        Dictionary<string, string> behaviorArguments,
        CancellationToken cancellationToken = default
        );
    Task StartTest(CancellationToken cancellationToken = default);
    Task Stop(CancellationToken cancellationToken = default);
}
