namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog;

using System.Threading.Tasks;
using AcceptanceTesting;
using NUnit.Framework;

public class When_catalog_with_special_characters_configured_for_endpoint : MultiCatalogAcceptanceTest
{
    static string EndpointConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "n service.bus&#");

    [Test]
    public async Task Should_be_able_to_send_messages_to_the_endpoint()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(c => c.When(s => s.SendLocal(new Message())))
            .Run();

        Assert.That(context.MessageReceived, Is.True);
    }

    public class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint() =>
            EndpointSetup(new CustomizedServer(EndpointConnectionString), (_, _) => { });

        class Handler(Context scenarioContext) : IHandleMessages<Message>
        {
            public Task Handle(Message message, IMessageHandlerContext context)
            {
                scenarioContext.MessageReceived = true;
                scenarioContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class Message : ICommand;

    class Context : ScenarioContext
    {
        public bool MessageReceived { get; set; }
    }
}