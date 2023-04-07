namespace TestRunner
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using TestComms;

    public class TestScenarioProcessRunner
    {
        public static async Task<TestExecutionResult> Run(string scenarioName, AgentInfo[] agents, CancellationToken cancellationToken = default)
        {
            var uniqueName = scenarioName + Guid.NewGuid().ToString("N");

            using var context = new MemoryMappedFileTestContext(uniqueName, true);

            var processes = agents.Select(x => new AgentProcess(x.Project, x.Behavior, uniqueName, x.BehaviorParameters ?? new Dictionary<string, string>())).ToArray();

            try
            {
                foreach (var agent in processes)
                {
                    agent.Start();
                }

                var finished = await context.WaitUntilTrue("Success", cancellationToken).ConfigureAwait(false);
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