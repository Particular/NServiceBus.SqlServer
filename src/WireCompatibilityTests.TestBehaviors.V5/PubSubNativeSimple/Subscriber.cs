using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Subscriber : Base, ITestBehavior, ISubscriber
{
    public Subscriber() : base("Subscriber")
    {
    }

    public override Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public class MyEventHandler : IHandleMessages<MyEvent>
    {
        public Task Handle(MyEvent message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}
