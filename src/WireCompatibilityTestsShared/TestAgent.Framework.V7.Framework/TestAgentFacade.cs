
namespace TestAgent.Framework
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using TestComms;
    using TestLogicApi;

    public class TestAgentFacade
    {
        public static async Task Run(string[] args, CancellationToken cancellationToken = default)
        {
            var behaviorClassName = args[0];
            var mappedFileName = args[1];

            var behaviorArgs = args.Skip(2).Select(x => x.Split('=')).ToDictionary(x => x[0], x => x.Length > 1 ? x[1] : null);

            var behaviorClass = Type.GetType(behaviorClassName, true);

            Console.Out.WriteLine($">> Creating {behaviorClass}");

            var behavior = (ITestBehavior)Activator.CreateInstance(behaviorClass);

            var config = behavior.Configure(behaviorArgs);

            var instance = await Endpoint.Start(config).ConfigureAwait(false);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var executionTask = behavior.Execute(instance);

            _ = await contextAccessor.WaitUntilTrue("Success", cancellationToken).ConfigureAwait(false);

            await instance.Stop().ConfigureAwait(false);
        }
    }
}
