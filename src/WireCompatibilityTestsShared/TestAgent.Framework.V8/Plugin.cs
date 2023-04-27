namespace TestAgent.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Customization;
    using TestLogicApi;

    public class Plugin : IPlugin
    {
        IEndpointInstance instance;

        public async Task Start(string behaviorClassName, Dictionary<string, string> behaviorArgs, CancellationToken cancellationToken = default)
        {
            var behaviorClass = Type.GetType(behaviorClassName, true);

            Console.Out.WriteLine($">> Creating {behaviorClass}");

            var behavior = (ITestBehavior)Activator.CreateInstance(behaviorClass);

            var config = behavior.Configure(behaviorArgs);
            config.TypesToIncludeInScan(GetTypesToScan(behaviorClass).ToList());

            instance = await Endpoint.Start(config, cancellationToken).ConfigureAwait(false);

            await behavior.Execute(instance).ConfigureAwait(false);
        }

        IEnumerable<Type> GetTypesToScan(Type behaviorType)
        {
            yield return behaviorType;
            foreach (var nested in behaviorType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                yield return nested;
            }
        }

        public Task Stop(CancellationToken cancellationToken = default) => instance.Stop(cancellationToken);
    }
}
