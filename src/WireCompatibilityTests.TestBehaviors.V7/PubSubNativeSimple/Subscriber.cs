using System.Threading.Tasks;
using NServiceBus;

class Subscriber : Base
{
    public Subscriber() : base(nameof(Subscriber))
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
