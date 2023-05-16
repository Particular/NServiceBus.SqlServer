﻿namespace NServiceBus.Transport.SqlServer.AcceptanceTests
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_using_encrypted_connection : NServiceBusAcceptanceTest
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
            Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                var transport = new SqlServerTransport(async cancellationToken =>
                {
                    var sqlConnectionStringBuilder = new SqlConnectionStringBuilder(GetConnectionString())
                    {
                        ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled
                    };

                    var connection = new SqlConnection(sqlConnectionStringBuilder.ToString());

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