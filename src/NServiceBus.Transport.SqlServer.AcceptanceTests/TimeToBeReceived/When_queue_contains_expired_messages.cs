﻿namespace NServiceBus.Transport.SqlServer.AcceptanceTests.TimeToBeReceived
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Configuration.AdvancedExtensibility;
    using Microsoft.Data.SqlClient;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_queue_contains_expired_messages : NServiceBusAcceptanceTest
    {
        [SetUp]
        public void SetUpConnectionString() =>
            connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

        [TestCase(TransportTransactionMode.TransactionScope)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.None)]
        public Task Should_remove_expired_messages_from_queue(TransportTransactionMode transactionMode)
        {
            if (transactionMode == TransportTransactionMode.TransactionScope && !OperatingSystem.IsWindows())
            {
                Assert.Ignore("Transaction scope mode is only supported on windows");
            }

            return Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.ConfigureSqlServerTransport().TransportTransactionMode = transactionMode;
                    });
                    b.When(async (bus, c) =>
                    {
                        await bus.SendLocal(new ExpiredMessage());
                        await bus.SendLocal(new Message());
                    });
                })
                .Done(c => c.MessageWasHandled && QueueIsEmpty())
                .Run();
        }

        bool QueueIsEmpty()
        {
            var endpoint = Conventions.EndpointNamingConvention(typeof(Endpoint));

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[{endpoint}]", connection))
                {
                    var numberOfMessagesInQueue = (int)command.ExecuteScalar();
                    return numberOfMessagesInQueue == 0;
                }
            }
        }

        class Context : ScenarioContext
        {
            public bool MessageWasHandled { get; set; }
        }

        class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    // Make sure the purger is fired often enough to clean expired messages from
                    // the queue before the test times out.
                    c.GetSettings().Set("SqlServer.PurgeTaskDelayTimeSpan", TimeSpan.FromSeconds(2));
                    c.LimitMessageProcessingConcurrencyTo(1);
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
                    scenarioContext.MessageWasHandled = true;
                    return Task.FromResult(0);
                }
            }
        }

        [TimeToBeReceived("00:00:00.001")]
        public class ExpiredMessage : IMessage
        {
        }

        public class Message : IMessage
        {
        }

        string connectionString;
    }
}