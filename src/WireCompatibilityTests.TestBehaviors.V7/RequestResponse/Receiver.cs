using System.Threading.Tasks;
using NServiceBus;

class SchemaReceiver : Receiver
{
    protected override void Configure(
        PluginOptions opts,
        EndpointConfiguration endpointConfig,
        SqlServerTransport transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig
    )
    {
        base.Configure(opts, endpointConfig, transportConfig, routingConfig);

        transportConfig.DefaultSchema = "receiver";
        transportConfig.SchemaAndCatalog.UseSchemaForQueue(opts.AuditQueue, "dbo");
    }
}

class Receiver : Base
{
    public class MyRequestHandler : IHandleMessages<MyRequest>
    {
        public Task Handle(MyRequest message, IMessageHandlerContext context)
        {
            return context.Reply(new MyResponse());
        }
    }
}
