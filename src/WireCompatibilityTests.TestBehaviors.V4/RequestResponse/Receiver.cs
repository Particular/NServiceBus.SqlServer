using System.Threading.Tasks;
using NServiceBus;

class Receiver : Base
{
    public Receiver() : base("Receiver")
    {
    }

    public class MyRequestHandler : IHandleMessages<MyRequest>
    {
        public Task Handle(MyRequest message, IMessageHandlerContext context)
        {
            return context.Reply(new MyResponse());
        }
    }
}