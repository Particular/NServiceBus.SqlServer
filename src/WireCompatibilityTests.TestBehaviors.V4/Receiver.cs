using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Receiver : Base, ITestBehavior
{
    protected Receiver() : base("Receiver")
    {
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