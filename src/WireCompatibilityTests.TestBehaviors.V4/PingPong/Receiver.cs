using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Receiver : Base, ITestBehavior
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