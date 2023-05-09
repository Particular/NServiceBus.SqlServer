using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class SchemaReceiver : Receiver
{
    protected override void Configure(Dictionary<string, string> args, EndpointConfiguration endpointConfig, SqlServerTransport transportConfig)
    {
        base.Configure(args, endpointConfig, transportConfig);

        transportConfig.DefaultSchema = "receiver";

        transportConfig.SchemaAndCatalog.UseSchemaForQueue("AuditSpy", "dbo");
    }
}

class Receiver : ITestBehavior
{
    public Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public EndpointConfiguration Configure(Dictionary<string, string> args)
    {
        var connectionString = args["ConnectionString"];

        var config = new EndpointConfiguration("Receiver");
        config.EnableInstallers();

        var transport = new SqlServerTransport(connectionString)
        {
            TransportTransactionMode = TransportTransactionMode.ReceiveOnly
        };

        config.UseTransport(transport);
        config.AuditProcessedMessagesTo("AuditSpy");

        Configure(args, config, transport);

        return config;
    }

    protected virtual void Configure(Dictionary<string, string> args, EndpointConfiguration endpointConfig,
        SqlServerTransport transportConfig)
    {
    }

    public class MyRequestHandler : IHandleMessages<MyRequest>
    {
        public Task Handle(MyRequest message, IMessageHandlerContext context)
        {
            return context.Reply(new MyResponse());
        }
    }
}
