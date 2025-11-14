namespace NServiceBus.TransportTests;

public partial class TransportTestsConfiguration
{
    public IConfigureTransportInfrastructure CreateTransportConfiguration() => new ConfigureSqlServerTransportInfrastructure();
}