namespace NServiceBus.SqlServer.AcceptanceTests
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Extensibility;
    using MultiCatalog;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport;

    public class When_providing_external_connection : MultiCatalogAcceptanceTest
    {
        [Test]
        public async Task Should_use_it()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>()
                .WithEndpoint<Sender>(b => b.When(async (bus, c) =>
                {
                    using (var connection = new SqlConnection(GetDefaultConnectionString()))
                    {
                        await connection.OpenAsync();
                        var transaction = connection.BeginTransaction();

                        var transportTransaction = new TransportTransaction();
                        transportTransaction.Set(connection);
                        transportTransaction.Set(transaction);

                        var options = new SendOptions();
                        options.GetExtensions().Set(transportTransaction);
                        await bus.Send(new Message(), options);

                        transaction.Commit();
                    }
                }))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.True(ctx.MessageReceived, "Message should be properly received");
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.SendOnly();
                    var transport = c.ConfigureTransport();
                    transport.Routing().RouteToEndpoint(typeof(Message), Conventions.EndpointNamingConvention(typeof(Receiver)));
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

            class Handler : IHandleMessages<Message>
            {
                public Context Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    Context.MessageReceived = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
        }

        public class Message : IMessage
        {
        }
    }
}