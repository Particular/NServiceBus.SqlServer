namespace ConsoleRunner
{
    using TestRunner;

    class Program
    {

        static async Task Main()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            //LoadingLogger.Active = true;
            var agents = new[]
            {
                AgentInfo.Create("V8", "Sender"),
                AgentInfo.Create("V8", "Receiver"),
            };

            var result = await TestScenarioPluginRunner.Run("Ping-Pong", agents, cts.Token).ConfigureAwait(false);

            Console.WriteLine(result.Succeeded);
        }
    }
}
