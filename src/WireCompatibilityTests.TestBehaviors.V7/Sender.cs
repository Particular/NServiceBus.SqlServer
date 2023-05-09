using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class SchemaSender : Sender
{
    protected override void Configure(Dictionary<string, string> args, EndpointConfiguration endpointConfig, SqlServerTransport transportConfig)
    {
        base.Configure(args, endpointConfig, transportConfig);

        transportConfig.DefaultSchema = "sender";
        transportConfig.SchemaAndCatalog.UseSchemaForQueue("AuditSpy", "dbo");
        transportConfig.SchemaAndCatalog.UseSchemaForQueue("Receiver", "receiver");
    }
}

class Sender : ITestBehavior
{
    public EndpointConfiguration Configure(Dictionary<string, string> args)
    {
        var connectionString = args["ConnectionString"];

        var config = new EndpointConfiguration("Sender");
        config.EnableInstallers();

        var transport = new SqlServerTransport(connectionString)
        {
            TransportTransactionMode = TransportTransactionMode.ReceiveOnly,
        };
        var routing = config.UseTransport(transport);
        routing.RouteToEndpoint(typeof(MyRequest), "Receiver");

        config.AuditProcessedMessagesTo("AuditSpy");

        Configure(args, config, transport);

        return config;
    }

    protected virtual void Configure(Dictionary<string, string> args, EndpointConfiguration endpointConfig,
        SqlServerTransport transportConfig)
    {
    }

    public async Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
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
