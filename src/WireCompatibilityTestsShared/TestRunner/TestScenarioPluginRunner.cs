namespace TestRunner
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using NServiceBus;

    public class TestScenarioPluginRunner
    {
        public static async Task<TestExecutionResult> Run(
            string scenarioName,
            AgentInfo[] agents,
            Func<Dictionary<string, AuditMessage>, bool> doneCallback,
            CancellationToken cancellationToken = default
            )
        {
            var uniqueName = scenarioName + Guid.NewGuid().ToString("N");

            var processes = agents.Select(x => new AgentPlugin(x.Project, x.Behavior, x.Plugin, x.BehaviorParameters ?? new Dictionary<string, string>())).ToArray();

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
                    await agent.Start(cancellationToken).ConfigureAwait(false);
                }

                var timeout = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

                var finished = await Task.WhenAny(timeout, done.Task).ConfigureAwait(false);

                if (finished == timeout)
                {
                    done.SetResult(false);
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
                    await agent.Stop(cancellationToken).ConfigureAwait(false);
                }
                if (endpoint != null)
                {
                    await endpoint.Stop(cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}