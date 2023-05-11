using System.Collections.Generic;
using NServiceBus;

class SchemaReceiver : Receiver
{
    protected override void Configure(Dictionary<string, string> args, EndpointConfiguration endpointConfig, TransportExtensions<SqlServerTransport> transportConfig)
    {
        base.Configure(args, endpointConfig, transportConfig);

        transportConfig.DefaultSchema("receiver");
        transportConfig.UseSchemaForQueue("AuditSpy", "dbo");
    }
}