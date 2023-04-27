
public interface IPlugin
{
    Task Start(string behaviorName, Dictionary<string, string> behaviorArguments, CancellationToken cancellationToken = default);
    Task Stop(CancellationToken cancellationToken = default);
}
