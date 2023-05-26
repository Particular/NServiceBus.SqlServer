using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Publisher : Base
{
    public Publisher() : base("Publisher")
    {
    }

    public override async Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        await endpointInstance.Publish(new MyEvent()).ConfigureAwait(false);
    }

    public class MyEventHandler : IHandleMessages<MyEvent>
    {
        public Task Handle(MyEvent message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}
