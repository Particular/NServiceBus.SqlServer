using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Sender : Base, ITestBehavior
{
    protected Sender() : base("Sender")
    {
    }

    protected override void Configure(Dictionary<string, string> args, EndpointConfiguration endpointConfig, TransportExtensions<SqlServerTransport> transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig)
    {
        routingConfig.RouteToEndpoint(typeof(MyRequest), "Receiver");
    }

    public override async Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        await endpointInstance.Send(new MyRequest()).ConfigureAwait(false);
    }

    public class MyResponseHandler : IHandleMessages<MyResponse>
    {
        public Task Handle(MyResponse message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}