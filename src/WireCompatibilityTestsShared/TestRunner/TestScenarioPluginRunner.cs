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
            PluginOptions opts,
            AgentInfo[] agents,
            TransportDefinition auditSpyTransport,
            Dictionary<string, string> platformSpecificAssemblies,
            Func<Dictionary<string, AuditMessage>, bool> doneCallback,
            CancellationToken cancellationToken = default
            )
        {
            var generatedFolderPath = FindGeneratedFolderPath();

            var processes = agents.Select(x => new AgentPlugin(
                platformSpecificAssemblies,
                x.Version,
                x.Behavior,
                generatedFolderPath,
                x.BehaviorParameters
                )).ToArray();

            var auditedMessages = new Dictionary<string, AuditMessage>();

            var sync = new object();

            var done = new TaskCompletionSource<bool>();

            Task OnMessage(MessageContext messageContext, IMessageDispatcher dispatcher, CancellationToken cancellationToken)
            {
                try
                {
                    Console.WriteLine($"Incoming audit message: {messageContext.NativeMessageId}");
                    if (messageContext.Headers.TryGetValue(nameof(opts.TestRunId), out var testRunIdHeader) &&
                        testRunIdHeader == opts.TestRunId)
                    {
                        var auditMessage = new AuditMessage(messageContext.NativeMessageId, messageContext.Headers, messageContext.Body);

                        lock (sync)
                        {
                            auditedMessages[messageContext.NativeMessageId] = auditMessage;
                            if (doneCallback(auditedMessages))
                            {
                                done.SetResult(true);
                            }
                        }
                    }
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\n===== ERROR: =====\n" + ex);
                    done.SetResult(false);
                    throw;
                }
            }

            var rawConfig = RawEndpointConfiguration.Create(
                opts.AuditQueue,
                auditSpyTransport,
                 OnMessage,
                 opts.AuditQueue + ".poison"
                 );

            rawConfig.AutoCreateQueues();
            IReceivingRawEndpoint endpoint = null;

            try
            {
                foreach (var agent in processes)
                {
                    await agent.Compile(cancellationToken).ConfigureAwait(false);
                }

                endpoint = await RawEndpoint.Start(rawConfig, cancellationToken).ConfigureAwait(false);

                foreach (var agent in processes)
                {
                    await agent.StartEndpoint(cancellationToken).ConfigureAwait(false);
                }

                var tests = new List<Task>();

                foreach (var agent in processes)
                {
                    tests.Add(agent.StartTest(cancellationToken));
                }

                var timeout = Task.Delay(30000, cancellationToken);

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