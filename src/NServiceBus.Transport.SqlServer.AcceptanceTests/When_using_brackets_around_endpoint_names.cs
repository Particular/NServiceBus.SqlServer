namespace NServiceBus.Transport.SqlServer.AcceptanceTests;

using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_using_brackets_around_endpoint_names : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_be_able_to_send_messages_to_self()
    {
        var ctx = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b => b.When((bus, c) => bus.SendLocal(new Message())))
            .Done(c => c.MessageReceived)
            .Run();

        Assert.That(ctx.MessageReceived, Is.True, "Message should be properly received");
    }

    public class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint()
        {
            EndpointSetup<DefaultServer>(c =>
            {
            }).CustomEndpointName("[SpecialCharacters]");
        }

        class Handler : IHandleMessages<Message>
        {
            readonly Context scenarioContext;
            public Handler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(Message message, IMessageHandlerContext context)
            {
                scenarioContext.MessageReceived = true;

                return Task.CompletedTask;
            }
        }
    }

    public class Context : ScenarioContext
    {
        public bool MessageReceived { get; set; }
    }

    public class Message : IMessage;
}