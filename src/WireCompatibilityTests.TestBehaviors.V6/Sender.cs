using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Sender : ITestBehavior
{
    public EndpointConfiguration Configure(Dictionary<string, string> args)
    {
        var connectionString = args["ConnectionString"];

        var config = new EndpointConfiguration("Sender");

        var transport = config.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connectionString);
        var routing = transport.Routing();
        routing.RouteToEndpoint(typeof(MyRequest), "Receiver");

        config.AuditProcessedMessagesTo("AuditSpy");

        return config;
    }

#pragma warning disable PS0018
    public async Task Execute(IEndpointInstance endpointInstance)
#pragma warning restore PS0018
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
