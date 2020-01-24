namespace NServiceBus.SqlServer.AcceptanceTests
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SqlServer;

    public class When_using_custom_connection_factory : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_use_provided_ready_to_use_connection()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When((bus, c) => bus.SendLocal(new Message())))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.True(ctx.MessageReceived, "Message should be properly received");
        }

        static string GetConnectionString()
        {
            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;";
            }
            return connectionString;
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.OverridePublicReturnAddress($"{Conventions.EndpointNamingConvention(typeof(Endpoint))}@dbo@nservicebus");
                    c.UseTransport<SqlServerTransport>()
                        .ConnectionString("this-will-not-work")
                        .UseCustomSqlConnectionFactory(async () =>
                        {
                            var connection = new SqlConnection(GetConnectionString());

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