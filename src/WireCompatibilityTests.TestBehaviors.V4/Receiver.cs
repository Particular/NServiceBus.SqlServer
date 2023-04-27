using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Receiver : ITestBehavior
{
#pragma warning disable PS0018
    public Task Execute(IEndpointInstance endpointInstance)
#pragma warning restore PS0018
    {
        return Task.CompletedTask;
    }

    public EndpointConfiguration Configure(Dictionary<string, string> args)
    {
        var config = new EndpointConfiguration("Receiver");

        config.UseTransport<LearningTransport>();
        config.AuditProcessedMessagesTo("AuditSpy");

        return config;
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
