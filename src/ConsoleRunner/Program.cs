namespace ConsoleRunner
{
    using TestRunner;

    class Program
    {
        static async Task Main()
        {
            var agents = new[]
            {
                AgentInfo.Create("V7"),
                AgentInfo.Create("V8"),
            };

            _ = await TestScenarioPluginRunner.Run("Ping-Pong", agents).ConfigureAwait(false);
        }
    }
}