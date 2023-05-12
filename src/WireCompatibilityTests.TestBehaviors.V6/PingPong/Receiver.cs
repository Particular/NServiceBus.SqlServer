using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Receiver : Base, ITestBehavior, IReceiver
{
    public Receiver() : base(nameof(Receiver))
    {
    }

    public class MyRequestHandler : IHandleMessages<MyRequest>
    {
#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        public Task Handle(MyRequest message, IMessageHandlerContext context)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            return context.Reply(new MyResponse());
        }
    }
}
