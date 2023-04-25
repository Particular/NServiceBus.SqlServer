namespace TestAgent.Framework
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using TestLogicApi;

    public class Plugin : IPlugin
    {
        IEndpointInstance instance;

        public async Task Start(string[] args, CancellationToken cancellationToken = default)
        {
            var behaviorClassName = args[0];
            var behaviorArgs = args.Skip(2).Select(x => x.Split('=')).ToDictionary(x => x[0], x => x.Length > 1 ? x[1] : null);

            var behaviorClass = Type.GetType(behaviorClassName, true);

            Console.Out.WriteLine($">> Creating {behaviorClass}");

            var behavior = (ITestBehavior)Activator.CreateInstance(behaviorClass);

            var config = behavior.Configure(behaviorArgs);

            instance = await Endpoint.Start(config, cancellationToken).ConfigureAwait(false);

            await behavior.Execute(instance).ConfigureAwait(false);
        }

        public Task Stop(CancellationToken cancellationToken = default) => instance.Stop(cancellationToken);
    }
}
