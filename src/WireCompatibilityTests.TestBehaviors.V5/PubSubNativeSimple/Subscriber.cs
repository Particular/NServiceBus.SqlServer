using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Subscriber : Base
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
