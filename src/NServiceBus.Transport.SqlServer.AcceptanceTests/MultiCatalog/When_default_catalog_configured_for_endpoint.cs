namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_default_catalog_configured_for_endpoint : MultiCatalogAcceptanceTest
    {
        [Test]
        public async Task It_creates_queues_in_the_correct_database()
        {
            var connectionString = WithCustomCatalog(GetDefaultConnectionString(), Context.SenderCatalog);
            using (var connection = new SqlConnection(connectionString))
            {
                // Confirm tables do not exist prior to starting the endpoint.
                if (await SqlUtilities.CheckIfTableExists(Context.SenderCatalog, "dbo", Context.SenderEndpointName, connection))
                {
                    await SqlUtilities.DropTable(Context.SenderCatalog, "dbo", Context.SenderEndpointName, connection);
                }

                var context = await Scenario.Define<Context>()
                                            .WithEndpoint<SenderWithCustomCatalog>(b => b.When(c => c.EndpointsStarted, async (s, ctx) =>
                                            {
                                                ctx.TablesFound = await SqlUtilities.CheckIfTableExists(Context.SenderCatalog, "dbo", Context.SenderEndpointName, connection);
                                                ctx.Finished = true;
                                            }))
                                            .Done(c => c.Finished)
                                            .Run();

                Assert.IsTrue(context.TablesFound);
            }
        }

        [Test]
        public async Task It_should_be_able_to_send_messages()
        {
            var context = await Scenario.Define<Context>()
                                        .WithEndpoint<SenderWithCustomCatalog>(c => c.When(s => s.Send(new Message())))
                                        .WithEndpoint<ReceiverWithCustomCatalog>()
                                        .Done(c => c.ReplyReceived)
                                        .Run();

            Assert.IsTrue(context.MessageReceived);
            Assert.IsTrue(context.ReplyReceived);
        }

        class Context : ScenarioContext
        {
            public static string SenderCatalog = "nservicebus1";
            public static string ReceiverCatalog = "nservicebus2";
            public static string ConnectionStringCatalog = "nservicebus";
            public static string SenderEndpointName = "default-catalog-test_sender";
            public static string ReceiverEndpointName = "default-catalog-test-receiver";

            public bool Finished { get; set; }
            public bool TablesFound { get; set; }

            public bool ReplyReceived { get; set; }
            public bool MessageReceived { get; set; }
        }

        class SenderWithCustomCatalog : EndpointConfigurationBuilder
        {
            public SenderWithCustomCatalog() =>
                _ = EndpointSetup<DefaultServer>(b =>
                {
                    var transport = new SqlServerTransport(WithCustomCatalog(GetDefaultConnectionString(), Context.ConnectionStringCatalog))
                    {
                        DefaultCatalog = Context.SenderCatalog
                    };

                    transport.SchemaAndCatalog.UseCatalogForQueue(Context.ReceiverEndpointName, Context.ReceiverCatalog);

                    var routing = b.UseTransport(transport);
                    routing.RouteToEndpoint(typeof(Message), Context.ReceiverEndpointName);
                })
                .CustomEndpointName(Context.SenderEndpointName);


            class Handler : IHandleMessages<Reply>
            {
                readonly Context scenarioContext;
                public Handler(Context scenarioContext) => this.scenarioContext = scenarioContext;

                public async Task Handle(Reply message, IMessageHandlerContext context)
                {
                    scenarioContext.ReplyReceived = true;
                    await Task.CompletedTask;
                }
            }
        }

        class ReceiverWithCustomCatalog : EndpointConfigurationBuilder
        {
            public ReceiverWithCustomCatalog() =>
                _ = EndpointSetup<DefaultServer>(b =>
                {
                    var transport = new SqlServerTransport(WithCustomCatalog(GetDefaultConnectionString(), Context.ConnectionStringCatalog))
                    {
                        DefaultCatalog = Context.ReceiverCatalog
                    };
                    transport.SchemaAndCatalog.UseCatalogForQueue(Context.SenderEndpointName, Context.SenderCatalog);
                    _ = b.UseTransport(transport);
                })
                .CustomEndpointName(Context.ReceiverEndpointName);

            class Handler : IHandleMessages<Message>
            {
                readonly Context scenarioContext;
                public Handler(Context scenarioContext) => this.scenarioContext = scenarioContext;

                public async Task Handle(Message message, IMessageHandlerContext context)
                {
                    scenarioContext.MessageReceived = true;
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
    }
}
