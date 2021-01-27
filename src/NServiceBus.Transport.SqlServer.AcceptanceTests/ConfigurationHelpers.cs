using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Transport;

static class ConfigurationHelpers
{
    public static SqlServerTransport ConfigureSqlServerTransport(this EndpointConfiguration configuration)
    {
        //TODO this is kind of a hack because the acceptance testing framework doesn't give any access to the transport definition to individual tests.
        return (SqlServerTransport) configuration.GetSettings().Get<TransportDefinition>();
    }
}