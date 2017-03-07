namespace CompatibilityTests.Common
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using Messages;

    public class EndpointFacadeBuilder
    {
        public static IEndpointFacade CreateAndConfigure<T>(EndpointDefinition endpointDefinition, string version, Action<T> config)
            where T : IEndpointConfiguration
        {
            var startupDirectory = new DirectoryInfo(Conventions.AssemblyDirectoryResolver(version));

            var appDomain = AppDomain.CreateDomain(
                startupDirectory.Name,
                null,
                new AppDomainSetup
                {
                    ApplicationBase = startupDirectory.FullName,
                    ConfigurationFile = Path.Combine(startupDirectory.FullName, $"EndpointV{version}.dll.config")
                });

            var assemblyPath = Conventions.AssemblyPathResolver(version);
            var typeName = Conventions.EndpointFacadeConfiguratorTypeNameResolver(version);

            var facade = (IEndpointFacade)appDomain.CreateInstanceFromAndUnwrap(assemblyPath, typeName);
            var configurator = (T) facade.Bootstrap(endpointDefinition);

            config(configurator);

            configurator.Start();

            return new MyWrapper(facade, appDomain);
        }
    }

    class MyWrapper : IEndpointFacade
    {
        IEndpointFacade facade;
        AppDomain domain;

        public MyWrapper(IEndpointFacade facade, AppDomain domain)
        {
            this.facade = facade;
            this.domain = domain;
        }

        public void Dispose()
        {
            try
            {
                facade.Dispose();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Could not dispose facade: {ex}");
            }
            try
            {
                AppDomain.Unload(domain);
            }
            catch (CannotUnloadAppDomainException exception)
            {
                Trace.TraceError($"Could not unload appdomain: {exception}");
            }
        }

        public IEndpointConfiguration Bootstrap(EndpointDefinition endpointDefinition)
        {
            throw new NotSupportedException("Bootstrapping is not supported at this stage.");
        }

        public void SendCommand(Guid messageId)
        {
            facade.SendCommand(messageId);
        }

        public void SendRequest(Guid requestId)
        {
            facade.SendRequest(requestId);
        }

        public void PublishEvent(Guid eventId)
        {
            facade.PublishEvent(eventId);
        }

        public void SendAndCallbackForInt(int value)
        {
            facade.SendAndCallbackForInt(value);
        }

        public void SendAndCallbackForEnum(CallbackEnum value)
        {
            facade.SendAndCallbackForEnum(value);
        }

        public Guid[] ReceivedMessageIds => facade.ReceivedMessageIds;
        public Guid[] ReceivedResponseIds => facade.ReceivedResponseIds;
        public Guid[] ReceivedEventIds => facade.ReceivedEventIds;
        public int[] ReceivedIntCallbacks => facade.ReceivedIntCallbacks;
        public CallbackEnum[] ReceivedEnumCallbacks => facade.ReceivedEnumCallbacks;
        public int NumberOfSubscriptions => facade.NumberOfSubscriptions;
    }
}
