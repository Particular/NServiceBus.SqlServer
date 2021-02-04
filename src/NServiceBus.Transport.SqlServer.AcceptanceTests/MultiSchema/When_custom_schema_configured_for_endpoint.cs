namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiSchema
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    public abstract class When_custom_schema_configured_for_endpoint : NServiceBusAcceptanceTest
    {
        public const string ReceiverSchema = "receiver";

        public class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var endpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Receiver));

                    c.ConfigureSqlServerTransport().SchemaAndCatalog.UseSchemaForQueue(endpointName, ReceiverSchema);
                });
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

                    return Task.FromResult(0);
                }
            }
        }

        public class Message : IMessage
        {
        }
    }
}