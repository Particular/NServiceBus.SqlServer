namespace CompatibilityTests.Common
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using Messages;

    public class EndpointFacadeBuilder
    {
        public static IEndpointFacade CreateAndConfigure<TEndpointDefinition>(TEndpointDefinition endpointDefinition, string version)
           where TEndpointDefinition : EndpointDefinition
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
            facade.Bootstrap(endpointDefinition);

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
            facade.Dispose();

            try
            {
                AppDomain.Unload(domain);
            }
            catch (CannotUnloadAppDomainException exception)
            {
                Trace.TraceError($"Could not unload appdomain: {exception}");
            }
        }

        public void Bootstrap(EndpointDefinition endpointDefinition)
        {
            facade.Bootstrap(endpointDefinition);
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
