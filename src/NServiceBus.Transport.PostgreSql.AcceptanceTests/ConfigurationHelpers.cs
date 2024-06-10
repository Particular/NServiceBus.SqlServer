using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Transport;
using NServiceBus.Transport.PostgreSql;

static class ConfigurationHelpers
{
    public static PostgreSqlTransport ConfigureSqlServerTransport(this EndpointConfiguration configuration)
    {
        return (PostgreSqlTransport)configuration.GetSettings().Get<TransportDefinition>();
    }

    public static string BuildAddressWithSchema(string endpointName, string schema)
    {
        return $"{schema}.{endpointName}";
    }

    public static string QuoteSchema(string schema)
    {
        return $"\"{schema}\"";
    }
}