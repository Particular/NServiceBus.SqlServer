
public interface IPlugin
{
    Task StartEndpoint(string behaviorName, Dictionary<string, string> behaviorArguments, string transportVersionString,
        CancellationToken cancellationToken = default);
    Task StartTest(CancellationToken cancellationToken = default);
    Task Stop(CancellationToken cancellationToken = default);
}
