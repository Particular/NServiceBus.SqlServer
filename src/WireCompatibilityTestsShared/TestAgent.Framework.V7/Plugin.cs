﻿
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
    using NServiceBus.Transport;
    using TestLogicApi;

    public class Plugin : IPlugin
    {
        IEndpointInstance instance;
        ITestBehavior behavior;

        public async Task StartEndpoint(string behaviorClassName, Dictionary<string, string> behaviorArgs,
            string transportVersionString, CancellationToken cancellationToken = default)
        {
            var behaviorClass = Type.GetType(behaviorClassName, true);

            Console.Out.WriteLine($">> Creating {behaviorClass}");

            behavior = (ITestBehavior)Activator.CreateInstance(behaviorClass);

            var config = behavior.Configure(behaviorArgs);
            config.TypesToIncludeInScan(GetTypesToScan(behaviorClass).ToList());
            config.Pipeline.Register(b => new StampVersionBehavior(b.Build<IDispatchMessages>()), "Stamps version");

            instance = await Endpoint.Start(config).ConfigureAwait(false);
        }

        IEnumerable<Type> GetTypesToScan(Type behaviorType)
        {
            yield return behaviorType;
            foreach (var nested in behaviorType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                yield return nested;
            }
        }

        public Task StartTest(CancellationToken cancellationToken = default) => behavior.Execute(instance, cancellationToken);

        public Task Stop(CancellationToken cancellationToken = default) => instance.Stop();
    }
}
