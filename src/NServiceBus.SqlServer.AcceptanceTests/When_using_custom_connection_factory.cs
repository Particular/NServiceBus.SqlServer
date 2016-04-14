namespace NServiceBus.SqlServer.AcceptanceTests
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NUnit.Framework;
    using Transports.SQLServer;

    public class When_using_custom_connection_factory : NServiceBusAcceptanceTest
    {
        [Test]
        public async void Should_use_provided_ready_to_use_connection()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When((bus, c) => bus.SendLocal(new Message())))
                .Done(c => c.MessageReceived)
                .Repeat(r => r.For(Transports.Default))
                .Should(c => Assert.True(c.MessageReceived, "Message should be properly received"))
                .Run();
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<SqlServerTransport>()
                        .ConnectionString("this-will-not-work")
                        .UseCustomSqlConnectionFactory(async () =>
                        {
                            var connection = new SqlConnection(@"Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;");

                            await connection.OpenAsync();

                            return connection;
                        });
                });
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