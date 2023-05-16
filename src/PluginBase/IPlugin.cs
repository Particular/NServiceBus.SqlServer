public interface IPlugin
{
    Task StartEndpoint(
        string behaviorName,
        PluginOptions opts,
        CancellationToken cancellationToken = default
        );
    Task StartTest(CancellationToken cancellationToken = default);
    Task Stop(CancellationToken cancellationToken = default);
}
