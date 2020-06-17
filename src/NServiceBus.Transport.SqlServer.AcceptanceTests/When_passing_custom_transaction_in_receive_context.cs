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
                    c.When(async bus => { await bus.SendLocal(new InitiatingMessage()); });
                })
                .Done(c => c.FollowUpMessageReceived)
                .Run(TimeSpan.FromSeconds(10));

            Assert.IsTrue(context.FollowUpMessageReceived);
        }

        class InitiatingMessage : IMessage
        {
        }
        
        class FollowUpMessage : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool FollowUpMessageReceived { get; set; }
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

            class ImmediateDispatchHandlers : IHandleMessages<FollowUpMessage>, IHandleMessages<InitiatingMessage>
            {
                public MyContext Context { get; set; }

                public Task Handle(FollowUpMessage message, IMessageHandlerContext context)
                {
                    Context.FollowUpMessageReceived = true;
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
                                await context.Send(new FollowUpMessage(), sendOptions);

                                transaction.Commit();
                            }
                        }
                        scope.Complete();
                    }

                    throw new Exception("This should NOT prevent the InitiatingMessage from failing.");
                }
            }
        }
    }
}