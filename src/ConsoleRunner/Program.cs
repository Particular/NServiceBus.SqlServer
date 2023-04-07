namespace ConsoleRunner
{
    using TestRunner;

    class Program
    {
        static async Task Main()
        {
            //LoadingLogger.Active = true;
            var agents = new[]
            {
                AgentInfo.Create("V8", "Sender"),
                AgentInfo.Create("V8", "Receiver"),
            };

            _ = await TestScenarioPluginRunner.Run("Ping-Pong", agents).ConfigureAwait(false);
        }
    }
}