using NServiceBus;

class SchemaSender : Sender
{
    public override void Configure(
        PluginOptions opts,
        EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig
        )
    {

        transportConfig.DefaultSchema("sender");
        transportConfig.UseSchemaForQueue(opts.AuditQueue, "dbo");
        transportConfig.UseSchemaForEndpoint(opts.ApplyUniqueRunPrefix(nameof(SchemaReceiver)), "receiver");

        var routing = transportConfig.Routing();
        routing.RouteToEndpoint(typeof(MyRequest), opts.ApplyUniqueRunPrefix(nameof(SchemaReceiver)));
    }
}
