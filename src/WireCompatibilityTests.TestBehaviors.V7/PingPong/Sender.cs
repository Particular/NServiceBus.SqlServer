using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class SchemaSender : Sender
{
    protected override void Configure(
        Dictionary<string, string> args,
        EndpointConfiguration endpointConfig,
        SqlServerTransport transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig
    )
    {
        base.Configure(args, endpointConfig, transportConfig, routingConfig);

        transportConfig.DefaultSchema = "sender";
        transportConfig.SchemaAndCatalog.UseSchemaForQueue(Keys.AuditQueue, "dbo");
        transportConfig.SchemaAndCatalog.UseSchemaForQueue(nameof(Receiver), "receiver");
    }
}

class Sender : Base, ITestBehavior
{
    public Sender() : base(nameof(Sender))
    {
    }

    protected override void Configure(
        Dictionary<string, string> args,
        EndpointConfiguration endpointConfig,
        SqlServerTransport transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig
    )
    {
        routingConfig.RouteToEndpoint(typeof(MyRequest), nameof(Receiver));
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
