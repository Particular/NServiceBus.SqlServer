﻿using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Sender : Base, ITestBehavior
{
    public Sender() : base(nameof(Sender))
    {
    }

    public override void Configure(
        PluginOptions opts,
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
        public Task Handle(MyResponse message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}
