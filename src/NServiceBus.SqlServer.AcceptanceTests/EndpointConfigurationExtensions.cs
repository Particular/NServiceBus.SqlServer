using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;

public static class EndpointConfigurationExtensions
{
    public static TransportExtensions<SqlServerTransport> ConfigureSqlServerTransport(this EndpointConfiguration endpointConfiguration)
    {
        return new TransportExtensions<SqlServerTransport>(endpointConfiguration.GetSettings());
    }
}
