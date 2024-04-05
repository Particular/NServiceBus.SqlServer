using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Transport;

static class ConfigurationHelpers
{
    public static SqlServerTransport ConfigureSqlServerTransport(this EndpointConfiguration configuration)
    {
        return (SqlServerTransport)configuration.GetSettings().Get<TransportDefinition>();
    }

    public static string BuildAddressWithSchema(string endpointName, string schema)
    {
        return $"{endpointName}@{schema}";
    }

    public static string QuoteSchema(string schema)
    {
        return $"[{schema}]";
    }
}