namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_default_catalog_configured_for_endpoint : MultiCatalogAcceptanceTest
    {
        static string ReceiverEndpoint => Conventions.EndpointNamingConvention(typeof(Receiver));

        [Test]
        public Task Should_be_able_to_send_messages()
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
                var transport = new SqlServerTransport
                {
                    ConnectionString = GetDefaultConnectionString(),
                    DefaultCatalog = "nservicebus1"
                };

                EndpointSetup(new CustomizedServer(transport), (c, sd) =>
                {
                    var routing = c.ConfigureRouting();

                    routing.RouteToEndpoint(typeof(Message), ReceiverEndpoint);
                });
            }


            class Handler : IHandleMessages<Reply>
            {
                readonly Context scenarioContext;
                public Handler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public async Task Handle(Reply message, IMessageHandlerContext context)
                {
                    scenarioContext.ReplyReceived = true;
                    scenarioContext.SenderDatabase = await Get.DatabaseName(context);

                    await Task.FromResult(0);
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                var transport = new SqlServerTransport
                {
                    ConnectionString = GetDefaultConnectionString(),
                    DefaultCatalog = "nservicebus1"
                };
                EndpointSetup(new CustomizedServer(transport), (c, sd) => { });
            }

            class Handler : IHandleMessages<Message>
            {
                Context scenarioContext;
                public Handler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public async Task Handle(Message message, IMessageHandlerContext context)
                {
                    scenarioContext.ReceiverDatabase = await Get.DatabaseName(context);
                    await context.Reply(new Reply());
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
            public string SenderDatabase { get; set; }
            public string ReceiverDatabase { get; set; }
        }

        public static class Get
        {
            public static async Task<string> DatabaseName(IMessageHandlerContext context)
            {
                await Task.Delay(1);
                return null;
            }
        }
    }
}