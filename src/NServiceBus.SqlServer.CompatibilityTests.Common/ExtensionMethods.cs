namespace NServiceBus.SqlServer.CompatibilityTests.Common
{
    using System;

    public static class ExtensionMethods
    {
        public static T As<T>(this object target)
        {
            return (T) target;
        }

        public static string TransportAddressForVersion(this SqlServerEndpointDefinition endpointDefinition, string version)
        {
            return version.StartsWith("1") 
                ? endpointDefinition.Name + "." + Environment.MachineName 
                : endpointDefinition.Name;
        }
    }
}
