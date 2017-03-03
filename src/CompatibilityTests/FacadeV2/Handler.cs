using NServiceBus;
using NServiceBus.SqlServer.CompatibilityTests.Common;
using NServiceBus.SqlServer.CompatibilityTests.Common.Messages;

public class Handler : IHandleMessages<TestCommand>, IHandleMessages<TestRequest>, IHandleMessages<TestResponse>, IHandleMessages<TestEvent>, IHandleMessages<TestIntCallback>, IHandleMessages<TestEnumCallback>
{
    public IBus Bus { get; set; }

    public MessageStore Store { get; set; }

    public void Handle(TestCommand command)
    {
        Store.Add<TestCommand>(command.Id);
    }

    public void Handle(TestRequest message)
    {
        Bus.Reply(new TestResponse { ResponseId = message.RequestId });
    }

    public void Handle(TestResponse message)
    {
        Store.Add<TestResponse>(message.ResponseId);
    }

    public void Handle(TestEvent message)
    {
        Store.Add<TestEvent>(message.EventId);
    }

    public void Handle(TestIntCallback message)
    {
        Bus.Return(message.Response);
    }

    public void Handle(TestEnumCallback message)
    {
        Bus.Return(message.CallbackEnum);
    }
}
