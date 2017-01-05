﻿namespace NServiceBus.SqlServer.AcceptanceTests.TimeToBeReceived
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NUnit.Framework;

    public class When_queue_contains_expired_messages : NServiceBusAcceptanceTest
    {
        [TestCase(TransportTransactionMode.TransactionScope)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.None)]
        public Task Should_remove_expired_messages_from_queue(TransportTransactionMode transactionMode)
        {
            return Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.UseTransport<SqlServerTransport>()
                            .Transactions(transactionMode);
                    });
                    b.When((bus, c) =>
                    {
                        bus.SendLocal(new ExpiredMessage());
                        bus.SendLocal(new Message());
                        return Task.FromResult(0);
                    });
                })
                .Done(c => c.MessageWasHandled && QueueIsEmpty())
                .Run();
        }

        bool QueueIsEmpty()
        {
            var endpoint = Conventions.EndpointNamingConvention(typeof(Endpoint));
            // TODO: Move opening SQL connection out of the method.
            using (var connection = new SqlConnection(@"Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;"))
            {
                connection.Open();
                using (var command = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[{endpoint}]", connection))
                {
                    var numberOfMessagesInQueue = (int) command.ExecuteScalar();
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
                public Context Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    Context.MessageWasHandled = true;
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
    }
}