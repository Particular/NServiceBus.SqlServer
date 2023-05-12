using System.Collections.Generic;
using NServiceBus;

class SchemaReceiver : Receiver, ISchemaReceiver
{
    public override void Configure(
        Dictionary<string, string> args,
        EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig
    )
    {
        base.Configure(args, endpointConfig, transportConfig);

        transportConfig.DefaultSchema(nameof(Receiver));
        transportConfig.UseSchemaForQueue(Keys.AuditQueue, "dbo");
    }
}
