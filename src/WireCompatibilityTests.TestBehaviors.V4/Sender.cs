using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Sender : ITestBehavior
{
    public EndpointConfiguration Configure(PluginOptions opts)
    {
        var config = new EndpointConfiguration("Sender");
        config.EnableInstallers();
        config.UsePersistence<InMemoryPersistence>();

        var transport = config.UseTransport<SqlServerTransport>();
        transport.ConnectionString(opts.ConnectionString);
        transport.Transactions(TransportTransactionMode.ReceiveOnly);

        var routing = transport.Routing();
        routing.RouteToEndpoint(typeof(MyRequest), "Receiver");

        config.AuditProcessedMessagesTo(opts.AuditQueue);

        return config;
    }

    public async Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        await endpointInstance.Send(new MyRequest()).ConfigureAwait(false);
    }

    public class MyResponseHandler : IHandleMessages<MyResponse>
    {
#pragma warning disable PS0018
        public Task Handle(MyResponse message, IMessageHandlerContext context)
#pragma warning restore PS0018
        {
            return Task.CompletedTask;
        }
    }
}
