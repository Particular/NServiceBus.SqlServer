using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class SchemaReceiver : Receiver
{
    protected override void Configure(Dictionary<string, string> args, EndpointConfiguration endpointConfig, TransportExtensions<SqlServerTransport> transportConfig)
    {
        base.Configure(args, endpointConfig, transportConfig);

        transportConfig.DefaultSchema("receiver");
        transportConfig.UseSchemaForQueue("AuditSpy", "dbo");
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
        config.UsePersistence<InMemoryPersistence>();

        var transport = config.UseTransport<SqlServerTransport>();
        transport.Transactions(TransportTransactionMode.ReceiveOnly);
        transport.ConnectionString(connectionString);

        config.AuditProcessedMessagesTo("AuditSpy");

        Configure(args, config, transport);

        return config;
    }

    protected virtual void Configure(Dictionary<string, string> args, EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig)
    {
    }

    public class MyRequestHandler : IHandleMessages<MyRequest>
    {
#pragma warning disable PS0018
        public Task Handle(MyRequest message, IMessageHandlerContext context)
#pragma warning restore PS0018
        {
            return context.Reply(new MyResponse());
        }
    }
}
