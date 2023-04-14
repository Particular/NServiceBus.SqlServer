namespace TestRunner
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;

    public class TestScenarioRunner
    {
#pragma warning disable PS0018
        public static async Task<TestExecutionResult> Run(string scenarioName, AgentInfo[] agents,
            Func<Dictionary<string, AuditMessage>, bool> doneCallback)
#pragma warning restore PS0018
        {
            var uniqueName = scenarioName + Guid.NewGuid().ToString("N");

            var processes = agents.Select(x => new AgentProcess(x.Project, x.Behavior, uniqueName, x.BehaviorParameters ?? new Dictionary<string, string>())).ToArray();

            var auditedMessages = new Dictionary<string, AuditMessage>();

            var sync = new object();

            var done = new TaskCompletionSource<bool>();

            var rawConfig = RawEndpointConfiguration.Create("AuditSpy", new LearningTransport(),
                 (messageContext, dispatcher, token) =>
                {
                    var auditMessage = new AuditMessage(messageContext.NativeMessageId, messageContext.Headers,
                        messageContext.Body);

                    lock (sync)
                    {
                        auditedMessages[messageContext.NativeMessageId] = auditMessage;
                        if (doneCallback(auditedMessages))
                        {
                            done.SetResult(true);
                        }
                    }

                    return Task.CompletedTask;
                }, "poison");
            IReceivingRawEndpoint endpoint = null;

            try
            {
                endpoint = await RawEndpoint.Start(rawConfig, CancellationToken.None).ConfigureAwait(false);

                foreach (var agent in processes)
                {
                    agent.Start();
                }

                var timeout = Task.Delay(TimeSpan.FromSeconds(30));

                var finished = await Task.WhenAny(timeout, done.Task).ConfigureAwait(false);

                if (finished == timeout)
                {
                    throw new Exception("Time timed out");
                }

                return new TestExecutionResult
                {
                    Succeeded = done.Task.IsCompleted,
                    AuditedMessages = auditedMessages
                };
            }
            finally
            {
                foreach (var agent in processes)
                {
                    await agent.Stop().ConfigureAwait(false);
                }
                if (endpoint != null)
                {
                    await endpoint.Stop().ConfigureAwait(false);
                }
            }
        }
    }
}