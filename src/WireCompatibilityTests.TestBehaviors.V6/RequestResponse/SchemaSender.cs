using NServiceBus;

class SchemaSender : Sender
{
    public override void Configure(
        PluginOptions opts,
        EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig
        )
    {
        base.Configure(opts, endpointConfig, transportConfig);

        transportConfig.DefaultSchema("sender");
        transportConfig.UseSchemaForQueue(opts.AuditQueue, "dbo");
        transportConfig.UseSchemaForEndpoint(opts.ApplyUniqueRunPrefix("Receiver"), "receiver");
    }
}
