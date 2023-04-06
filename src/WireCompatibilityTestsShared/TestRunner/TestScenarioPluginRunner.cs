namespace TestRunner
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using TestComms;

    public class TestScenarioPluginRunner
    {
#pragma warning disable PS0018
        public static async Task<TestExecutionResult> Run(
            string scenarioName,
            AgentInfo[] agents
            )
#pragma warning restore PS0018
        {
            var uniqueName = scenarioName + Guid.NewGuid().ToString("N");

            using var context = new MemoryMappedFileTestContext(uniqueName, true);

            var processes = agents.Select(x => new AgentPlugin(x.Project, x.Behavior, uniqueName, x.BehaviorParameters ?? new Dictionary<string, string>())).ToArray();

            try
            {
                var tasks = new List<Task>();
                foreach (var agent in processes)
                {
                    tasks.Add(agent.Start());
                }

                var finished = await context.WaitUntilTrue("Success", TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                var variables = context.ToDictionary();
                return new TestExecutionResult
                {
                    Succeeded = finished,
                    VariableValues = variables
                };
            }
            finally
            {
                foreach (var agent in processes)
                {
                    await agent.Stop().ConfigureAwait(false);
                }
            }
        }
    }
}