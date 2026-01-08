namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog;

using System.Threading.Tasks;
using AcceptanceTesting;
using Microsoft.Data.SqlClient;
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
                .WithEndpoint<SenderWithCustomCatalog>(b => b.When(async (s, ctx) =>
                {
                    ctx.TablesFound = await SqlUtilities.CheckIfTableExists(Context.SenderCatalog, "dbo", Context.SenderEndpointName, connection);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(context.TablesFound, Is.True);
        }
    }

    [Test]
    public async Task It_should_be_able_to_send_messages()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<SenderWithCustomCatalog>(c => c.When(s => s.Send(new Message())))
            .WithEndpoint<ReceiverWithCustomCatalog>()
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.MessageReceived, Is.True);
            Assert.That(context.ReplyReceived, Is.True);
        }
    }

    class Context : ScenarioContext
    {
        public static string SenderCatalog = "nservicebus1";
        public static string ReceiverCatalog = "nservicebus2";
        public static string ConnectionStringCatalog = "nservicebus";
        public static string SenderEndpointName = "default-catalog-test_sender";
        public static string ReceiverEndpointName = "default-catalog-test-receiver";

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

        class Handler(Context scenarioContext) : IHandleMessages<Reply>
        {
            public async Task Handle(Reply message, IMessageHandlerContext context)
            {
                scenarioContext.ReplyReceived = true;
                scenarioContext.MarkAsCompleted();
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

        class Handler(Context scenarioContext) : IHandleMessages<Message>
        {
            public async Task Handle(Message message, IMessageHandlerContext context)
            {
                scenarioContext.MessageReceived = true;
                await context.Reply(new Reply());
            }
        }
    }

    public class Message : ICommand;

    public class Reply : IMessage;
}