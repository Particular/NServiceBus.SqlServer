using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Receiver : Base, ITestBehavior
{
    public override Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public class MyRequestHandler : IHandleMessages<MyRequest>
    {
        public Task Handle(MyRequest message, IMessageHandlerContext context)
        {
            return context.Reply(new MyResponse());
        }
    }
}
