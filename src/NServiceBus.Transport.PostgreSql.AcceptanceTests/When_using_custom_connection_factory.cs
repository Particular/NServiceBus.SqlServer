namespace NServiceBus.Transport.PostgreSql.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Npgsql;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_using_custom_connection_factory : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_use_provided_ready_to_use_connection()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When((bus, c) => bus.SendLocal(new Message())))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.That(ctx.MessageReceived, Is.True, "Message should be properly received");
        }

        static string GetConnectionString() =>
            Environment.GetEnvironmentVariable("PostgreSqlTransportConnectionString") ?? @"User ID=user;Password=admin;Host=localhost;Port=54320;Database=nservicebus;Pooling=true;Connection Lifetime=0;Include Error Detail=true";

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                var transport = new PostgreSqlTransport(async cancellationToken =>
                {
                    var connection = new NpgsqlConnection(GetConnectionString());

                    await connection.OpenAsync(cancellationToken);

                    return connection;
                });

                EndpointSetup(new CustomizedServer(transport), (c, sd) =>
                {
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

        public class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
        }

        public class Message : IMessage
        {
        }
    }
}
