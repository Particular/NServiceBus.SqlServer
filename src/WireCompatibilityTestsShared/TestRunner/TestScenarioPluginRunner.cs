namespace TestRunner
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using System.IO;
    using NServiceBus.Transport;

    public class TestScenarioPluginRunner
    {
        public static async Task<TestExecutionResult> Run(
            AgentInfo[] agents,
            TransportDefinition auditSpyTransport,
            Dictionary<string, string> platformSpecificAssemblies,
            Func<Dictionary<string, AuditMessage>, bool> doneCallback,
            CancellationToken cancellationToken = default)
        {
            var generatedFolderPath = FindGeneratedFolderPath();

            var processes = agents.Select(x => new AgentPlugin(platformSpecificAssemblies, x.Major, x.Minor, x.CoreMajor, x.Behavior, generatedFolderPath, x.BehaviorParameters ?? new Dictionary<string, string>())).ToArray();

            var auditedMessages = new Dictionary<string, AuditMessage>();

            var sync = new object();

            var done = new TaskCompletionSource<bool>();

            var rawConfig = RawEndpointConfiguration.Create("AuditSpy", auditSpyTransport,
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
            rawConfig.AutoCreateQueues();
            IReceivingRawEndpoint endpoint = null;

            try
            {
                foreach (var agent in processes)
                {
                    await agent.Compile().ConfigureAwait(false);
                }

                endpoint = await RawEndpoint.Start(rawConfig, cancellationToken).ConfigureAwait(false);

                foreach (var agent in processes)
                {
                    await agent.StartEndpoint(cancellationToken).ConfigureAwait(false);
                }

                foreach (var agent in processes)
                {
                    await agent.StartTest(cancellationToken).ConfigureAwait(false);
                }

                var timeout = Task.Delay(-1, cancellationToken);

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

        static string FindGeneratedFolderPath()
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;

            while (true)
            {
                // Finding a solution file takes precedence
                if (Directory.EnumerateFiles(directory).Any(file => file.EndsWith(".sln")))
                {
                    return Path.Combine(directory, DefaultDirectory);
                }

                // When no solution file was found try to find a learning transport directory
                var learningTransportDirectory = Path.Combine(directory, DefaultDirectory);
                if (Directory.Exists(learningTransportDirectory))
                {
                    return learningTransportDirectory;
                }

                var parent = Directory.GetParent(directory) ?? throw new Exception($"Unable to determine the storage directory path for the learning transport due to the absence of a solution file. Either create a '{DefaultDirectory}' directory in one of this project’s parent directories, or specify the path explicitly using the 'EndpointConfiguration.UseTransport<LearningTransport>().StorageDirectory()' API.");

                directory = parent.FullName;
            }
        }

        const string DefaultDirectory = ".wirecompattests";
    }
}