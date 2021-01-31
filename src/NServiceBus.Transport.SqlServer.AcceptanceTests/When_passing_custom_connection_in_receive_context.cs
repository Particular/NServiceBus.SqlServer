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

    public class When_passing_custom_connection_in_receive_context : NServiceBusAcceptanceTest
    {
        static string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");

        [Test]
        public async Task Should_use_connection_in_transport_operations()
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
                .Done(c => c.FollowUpCommittedCommandReceived && c.FollowUpCommittedCommandReceived)
                .Run(TimeSpan.FromSeconds(10));

            Assert.IsFalse(context.FollowUpRolledbackCommandReceived);
            Assert.IsFalse(context.FollowUpRolledbackEventReceived);
            Assert.IsFalse(context.InHandlerTransactionEscalatedToDTC);
        }

        class InitiatingMessage : IMessage
        {
        }

        class FollowUpCompletedCommand : IMessage
        {
        }

        class FollowUpCompletedEvent : IEvent
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

            public bool InHandlerTransactionEscalatedToDTC { get; set; }
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
                IHandleMessages<FollowUpCompletedCommand>,
                IHandleMessages<FollowUpCompletedEvent>,
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
                    //HINT: this scope is never completed
                    using (new System.Transactions.TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
                    {
                        using (var connection = new SqlConnection(ConnectionString))
                        {
                            connection.Open();

                            var sendOptions = new SendOptions();
                            sendOptions.UseCustomSqlConnection(connection);
                            sendOptions.RouteToThisEndpoint();
                            await context.Send(new FollowUpRolledbackCommand(), sendOptions);

                            var publishOptions = new PublishOptions();
                            publishOptions.UseCustomSqlConnection(connection);
                            await context.Publish(new FollowUpRolledbackEvent(), publishOptions);
                        }
                    }

                    using (var scope = new System.Transactions.TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
                    {
                        using (var connection = new SqlConnection(ConnectionString))
                        {
                            connection.Open();

                            var sendOptions = new SendOptions();
                            sendOptions.UseCustomSqlConnection(connection);
                            sendOptions.RouteToThisEndpoint();
                            await context.Send(new FollowUpCompletedCommand(), sendOptions);

                            var publishOptions = new PublishOptions();
                            publishOptions.UseCustomSqlConnection(connection);
                            await context.Publish(new FollowUpCompletedEvent(), publishOptions);

                            scenarioContext.InHandlerTransactionEscalatedToDTC = Transaction.Current.TransactionInformation.DistributedIdentifier != Guid.Empty;
                        }

                        scope.Complete();
                    }

                    throw new Exception("This should NOT prevent the InitiatingMessage from failing.");
                }

                public Task Handle(FollowUpCompletedEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.FollowUpCommittedEventReceived = true;
                    return Task.CompletedTask;
                }

                public Task Handle(FollowUpCompletedCommand completedCommand, IMessageHandlerContext context)
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