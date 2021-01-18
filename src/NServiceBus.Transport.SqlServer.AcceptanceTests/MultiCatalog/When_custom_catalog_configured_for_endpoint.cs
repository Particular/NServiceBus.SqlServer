namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
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
        public Task Should_be_able_to_send_message_to_input_queue_in_different_catalog()
        {
            return Scenario.Define<Context>()
                .WithEndpoint<Sender>(c => c.When(s => s.Send(new Message())))
                .WithEndpoint<Receiver>()
                .Done(c => c.ReplyReceived)
                .Run();
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var transport = new SqlServerTransport
                    {
                        ConnectionString = SenderConnectionString
                    };
                    transport.EndpointSchemaAndCatalogSettings.SpecifyCatalog(ReceiverEndpoint, "nservicebus2");
                    c.UseTransport(transport);

                    c.ConfigureRouting().RouteToEndpoint(typeof(Message), ReceiverEndpoint);

                });
            }


            class Handler : IHandleMessages<Reply>
            {
                private readonly Context scenarioContext;
                public Handler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(Reply message, IMessageHandlerContext context)
                {
                    scenarioContext.ReplyReceived = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport(new SqlServerTransport {ConnectionString = ReceiverConnectionString});
                });
            }

            class Handler : IHandleMessages<Message>
            {
                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    return context.Reply(new Reply());
                }
            }
        }

        public class Message : ICommand
        {
        }

        public class Reply : IMessage
        {
        }

        class Context : ScenarioContext
        {
            public bool ReplyReceived { get; set; }
        }
    }
}