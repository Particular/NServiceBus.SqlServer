using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Transport;

static class ConfigurationHelpers
{
    public static SqlServerTransport ConfigureSqlServerTransport(this EndpointConfiguration configuration)
    {
        return (SqlServerTransport)configuration.GetSettings().Get<TransportDefinition>();
    }
}