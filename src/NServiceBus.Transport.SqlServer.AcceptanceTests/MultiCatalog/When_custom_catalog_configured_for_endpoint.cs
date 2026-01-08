namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog;

using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_custom_catalog_configured_for_endpoint : MultiCatalogAcceptanceTest
{
    static string SenderConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus1");
    static string ReceiverConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus2");
    static string ReceiverEndpoint => Conventions.EndpointNamingConvention(typeof(Receiver));

    [Test]
    public async Task Should_be_able_to_send_message_to_input_queue_in_different_catalog()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Sender>(c => c.When(s => s.Send(new Message())))
            .WithEndpoint<Receiver>()
            .Run();

        Assert.That(context.ReplyReceived, Is.True);
    }

    public class Sender : EndpointConfigurationBuilder
    {
        public Sender() =>
            EndpointSetup(new CustomizedServer(SenderConnectionString), (c, sd) =>
            {
                var routing = c.ConfigureRouting();

                routing.RouteToEndpoint(typeof(Message), ReceiverEndpoint);
                routing.UseCatalogForEndpoint(ReceiverEndpoint, "nservicebus2");
            });

        class Handler(Context scenarioContext) : IHandleMessages<Reply>
        {
            public Task Handle(Reply message, IMessageHandlerContext context)
            {
                scenarioContext.ReplyReceived = true;
                scenarioContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() => EndpointSetup(new CustomizedServer(ReceiverConnectionString), (c, sd) => { });

        class Handler : IHandleMessages<Message>
        {
            public Task Handle(Message message, IMessageHandlerContext context) => context.Reply(new Reply());
        }
    }

    public class Message : ICommand;

    public class Reply : IMessage;

    class Context : ScenarioContext
    {
        public bool ReplyReceived { get; set; }
    }
}