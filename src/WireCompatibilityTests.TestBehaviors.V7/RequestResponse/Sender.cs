using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class SchemaSender : Sender
{
    protected override void Configure(
        PluginOptions opts,
        EndpointConfiguration endpointConfig,
        SqlServerTransport transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig
    )
    {
        base.Configure(opts, endpointConfig, transportConfig, routingConfig);

        transportConfig.DefaultSchema = "sender";
        transportConfig.SchemaAndCatalog.UseSchemaForQueue(opts.AuditQueue, "dbo");
        transportConfig.SchemaAndCatalog.UseSchemaForQueue(opts.ApplyUniqueRunPrefix(nameof(Receiver)), "receiver");
    }
}

class Sender : Base
{
    public Sender() : base(nameof(Sender))
    {
    }

    protected override void Configure(
        PluginOptions opts,
        EndpointConfiguration endpointConfig,
        SqlServerTransport transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig
    )
    {
        routingConfig.RouteToEndpoint(typeof(MyRequest), opts.ApplyUniqueRunPrefix(nameof(Receiver)));
    }

    public override async Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        await endpointInstance.Send(new MyRequest(), cancellationToken).ConfigureAwait(false);
    }

    public class MyResponseHandler : IHandleMessages<MyResponse>
    {
        public Task Handle(MyResponse message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}
