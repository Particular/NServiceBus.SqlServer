namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NUnit.Framework;

    public class When_catalog_with_special_characters_configured_for_endpoint : MultiCatalogAcceptanceTest
    {
        static string EndpointConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "n service.bus&*#");

        [Test]
        public async Task Should_be_able_to_send_messages_to_the_endpoint()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(c => c.When(s => s.SendLocal(new Message())))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.Pass();
        }

        public class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint() =>
                EndpointSetup(new CustomizedServer(EndpointConnectionString), (c, sd) =>
                {
                });


            class Handler(Context scenarioContext) : IHandleMessages<Message>
            {
                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    scenarioContext.MessageReceived = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class Message : ICommand
        {
        }

        class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
        }
    }
}