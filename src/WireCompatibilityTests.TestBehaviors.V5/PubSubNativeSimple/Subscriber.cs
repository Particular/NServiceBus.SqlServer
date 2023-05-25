using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Subscriber : Base, ITestBehavior, ISubscriber
{
    public Subscriber() : base("Subscriber")
    {
    }

    public class MyEventHandler : IHandleMessages<MyEvent>
    {
        public Task Handle(MyEvent message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}
