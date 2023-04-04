﻿
namespace TestAgent.Framework
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using TestComms;
    using TestLogicApi;

    public class TestAgentFacade
    {
#pragma warning disable PS0018
        public static async Task Run(string[] args)
#pragma warning restore PS0018
        {
            var behaviorClassName = args[0];
            var mappedFileName = args[1];

            var behaviorArgs = args.Skip(2).Select(x => x.Split('=')).ToDictionary(x => x[0], x => x.Length > 1 ? x[1] : null);

            var behaviorClass = Type.GetType(behaviorClassName, true);

            var behavior = (ITestBehavior)Activator.CreateInstance(behaviorClass);

            var config = behavior.Configure(behaviorArgs);

            var contextAccessor = new MemoryMappedFileTestContext(mappedFileName);

            config.RegisterComponents(cc =>
            {
                cc.RegisterSingleton(typeof(ITestContextAccessor), contextAccessor);
            });

            var instance = await Endpoint.Start(config).ConfigureAwait(false);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var succeededTask = contextAccessor.WaitUntilTrue("Success", TimeSpan.FromSeconds(30));
            var executionTask = behavior.Execute(instance);

            var firstCompleted = await Task.WhenAny(timeoutTask, succeededTask).ConfigureAwait(false);

            await instance.Stop().ConfigureAwait(false);
        }
    }
}
