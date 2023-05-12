using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Sender : Base, ITestBehavior
{
    public Sender() : base(nameof(Sender))
    {
    }

    public override void Configure(
        Dictionary<string, string> args,
        EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig
        )
    {
        var routing = transportConfig.Routing();
        routing.RouteToEndpoint(typeof(MyRequest), nameof(Receiver));
    }

    public override async Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
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
