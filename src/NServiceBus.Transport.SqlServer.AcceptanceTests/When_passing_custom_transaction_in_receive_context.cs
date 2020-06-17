namespace NServiceBus.Transport.SqlServer.AcceptanceTests
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using System.Transactions;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_passing_custom_transaction_in_receive_context : NServiceBusAcceptanceTest
    {
        static string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");

        [Test]
        public async Task Should_use_transaction_in_transport_operations()
        {
            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<AnEndpoint>(c =>
                {
                    c.DoNotFailOnErrorMessages();
                    c.When(async bus =>
                    {
                        await bus.SendLocal(new InitiatingMessage());
                    });
                })
                .Done(c => c.FollowUpCommandReceived)
                .Run(TimeSpan.FromSeconds(10));

            Assert.IsTrue(context.FollowUpCommandReceived);
        }

        class InitiatingMessage : IMessage
        {
        }
        
        class FollowUpCommand : IMessage
        {
        }

        class FollowUpEvent : IEvent
        {
        }

        class MyContext : ScenarioContext
        {
            public bool FollowUpCommandReceived { get; set; }
            public bool FollowUpEventReceived { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.LimitMessageProcessingConcurrencyTo(1);
                });
            }

            class ImmediateDispatchHandlers : IHandleMessages<InitiatingMessage>,
                IHandleMessages<FollowUpCommand>, 
                IHandleMessages<FollowUpEvent>
            {
                public MyContext Context { get; set; }

                public Task Handle(FollowUpCommand command, IMessageHandlerContext context)
                {
                    Context.FollowUpCommandReceived = true;
                    return Task.CompletedTask;
                }

                public async Task Handle(InitiatingMessage message, IMessageHandlerContext context)
                {
                    using (var scope = new System.Transactions.TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
                    {
                        using (var connection = new SqlConnection(ConnectionString))
                        {
                            connection.Open();
                            using (var transaction = connection.BeginTransaction())
                            {
                                var sendOptions = new SendOptions();
                                sendOptions.UseCustomSqlTransaction(transaction);
                                sendOptions.RouteToThisEndpoint();
                                await context.Send(new FollowUpCommand(), sendOptions);

                                var publishOptions = new PublishOptions();
                                publishOptions.UseCustomSqlTransaction(transaction);
                                await context.Publish(new FollowUpEvent(), publishOptions);

                                transaction.Commit();
                            }
                        }
                        scope.Complete();
                    }

                    throw new Exception("This should NOT prevent the InitiatingMessage from failing.");
                }

                public Task Handle(FollowUpEvent message, IMessageHandlerContext context)
                {
                    Context.FollowUpEventReceived = true;

                    return Task.CompletedTask;
                }
            }
        }
    }
}