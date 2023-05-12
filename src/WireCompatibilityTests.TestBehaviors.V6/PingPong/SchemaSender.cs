using System.Collections.Generic;
using NServiceBus;

class SchemaSender : Sender
{
    public override void Configure(Dictionary<string, string> args, EndpointConfiguration endpointConfig, TransportExtensions<SqlServerTransport> transportConfig)
    {
        base.Configure(args, endpointConfig, transportConfig);

        transportConfig.DefaultSchema("sender");
        transportConfig.UseSchemaForQueue("AuditSpy", "dbo");
        transportConfig.UseSchemaForEndpoint("Receiver", "receiver");
    }
}
