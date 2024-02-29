﻿namespace NServiceBus.Transport.SqlServer.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Microsoft.Data.SqlClient;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_using_column_encrypted_connection : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_work()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When((bus, c) => bus.SendLocal(new Message())))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.True(ctx.MessageReceived, "Message should be properly received");
        }

        static string GetConnectionString() =>
            Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                var transport = new SqlServerTransport(async cancellationToken =>
                {
                    var connectionString = GetConnectionString();

                    if (!connectionString.EndsWith(";"))
                    {
                        connectionString += ";";
                    }

                    connectionString += "Column Encryption Setting=enabled";

                    var connection = new SqlConnection(connectionString);

                    await connection.OpenAsync(cancellationToken);

                    return connection;
                });

                EndpointSetup(new CustomizedServer(transport), (c, sd) =>
                {
                    c.OverridePublicReturnAddress($"{Conventions.EndpointNamingConvention(typeof(Endpoint))}@dbo@nservicebus");
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
