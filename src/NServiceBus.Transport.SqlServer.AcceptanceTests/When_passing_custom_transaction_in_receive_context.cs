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
                .Done(c => c.FollowUpCommittedCommandReceived && c.FollowUpCommittedEventReceived)
                .Run(TimeSpan.FromSeconds(10));

            Assert.IsFalse(context.FollowUpRolledbackCommandReceived);
            Assert.IsFalse(context.FollowUpRolledbackEventReceived);
        }

        class InitiatingMessage : IMessage
        {
        }

        class FollowUpCommittedCommand : IMessage
        {
        }

        class FollowUpCommittedEvent : IEvent
        {
        }

        class FollowUpRolledbackCommand : IMessage
        {
        }

        class FollowUpRolledbackEvent : IEvent
        {
        }

        class MyContext : ScenarioContext
        {
            public bool FollowUpCommittedCommandReceived { get; set; }
            public bool FollowUpCommittedEventReceived { get; set; }
            public bool FollowUpRolledbackCommandReceived { get; set; }
            public bool FollowUpRolledbackEventReceived { get; set; }
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
                IHandleMessages<FollowUpCommittedCommand>,
                IHandleMessages<FollowUpCommittedEvent>,
                IHandleMessages<FollowUpRolledbackCommand>,
                IHandleMessages<FollowUpRolledbackEvent>
            {
                readonly MyContext scenarioContext;
                public ImmediateDispatchHandlers(MyContext scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public async Task Handle(InitiatingMessage message, IMessageHandlerContext context)
                {
                    using (var scope = new System.Transactions.TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
                    {
                        using (var connection = new SqlConnection(ConnectionString))
                        {
                            connection.Open();

                            using (var rollbackedTransaction = connection.BeginTransaction())
                            {
                                var sendOptions = new SendOptions();
                                sendOptions.UseCustomSqlTransaction(rollbackedTransaction);
                                sendOptions.RouteToThisEndpoint();
                                await context.Send(new FollowUpRolledbackCommand(), sendOptions);

                                var publishOptions = new PublishOptions();
                                publishOptions.UseCustomSqlTransaction(rollbackedTransaction);
                                await context.Publish(new FollowUpRolledbackEvent(), publishOptions);

                                rollbackedTransaction.Rollback();
                            }

                            using (var transaction = connection.BeginTransaction())
                            {
                                var sendOptions = new SendOptions();
                                sendOptions.UseCustomSqlTransaction(transaction);
                                sendOptions.RouteToThisEndpoint();
                                await context.Send(new FollowUpCommittedCommand(), sendOptions);

                                var publishOptions = new PublishOptions();
                                publishOptions.UseCustomSqlTransaction(transaction);
                                await context.Publish(new FollowUpCommittedEvent(), publishOptions);

                                transaction.Commit();
                            }
                        }
                        scope.Complete();
                    }

                    throw new Exception("This should NOT prevent follow-up messages from being sent.");
                }

                public Task Handle(FollowUpCommittedEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.FollowUpCommittedEventReceived = true;

                    return Task.CompletedTask;
                }

                public Task Handle(FollowUpCommittedCommand message, IMessageHandlerContext context)
                {
                    scenarioContext.FollowUpCommittedCommandReceived = true;
                    return Task.CompletedTask;
                }

                public Task Handle(FollowUpRolledbackCommand message, IMessageHandlerContext context)
                {
                    scenarioContext.FollowUpRolledbackCommandReceived = true;
                    return Task.CompletedTask;
                }

                public Task Handle(FollowUpRolledbackEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.FollowUpRolledbackEventReceived = true;
                    return Task.CompletedTask;
                }
            }
        }
    }
}