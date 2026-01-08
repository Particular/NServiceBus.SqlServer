namespace NServiceBus.Transport.SqlServer.AcceptanceTests;

using System;
using System.Threading.Tasks;
using System.Transactions;
using AcceptanceTesting;
using Microsoft.Data.SqlClient;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_passing_custom_connection_in_receive_context : NServiceBusAcceptanceTest
{
    static readonly string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

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
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(context.FollowUpRolledbackCommandReceived, Is.False);
            Assert.That(context.FollowUpRolledbackEventReceived, Is.False);
            Assert.That(context.InHandlerTransactionEscalatedToDTC, Is.False);
        });
    }

    class InitiatingMessage : IMessage;

    class FollowUpCompletedCommand : IMessage;

    class FollowUpCompletedEvent : IEvent;

    class FollowUpRolledbackCommand : IMessage;

    class FollowUpRolledbackEvent : IEvent;

    class MyContext : ScenarioContext
    {
        public bool FollowUpCommittedCommandReceived { get; set; }
        public bool FollowUpCommittedEventReceived { get; set; }
        public bool FollowUpRolledbackCommandReceived { get; set; }
        public bool FollowUpRolledbackEventReceived { get; set; }
        public bool InHandlerTransactionEscalatedToDTC { get; set; }

        public void MaybeMarkAsCompleted() => MarkAsCompleted(FollowUpCommittedCommandReceived, FollowUpCommittedEventReceived);
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint() =>
            EndpointSetup<DefaultServer>(c =>
            {
                c.LimitMessageProcessingConcurrencyTo(1);
            });

        class ImmediateDispatchHandlers(MyContext scenarioContext) : IHandleMessages<InitiatingMessage>,
            IHandleMessages<FollowUpCompletedCommand>,
            IHandleMessages<FollowUpCompletedEvent>,
            IHandleMessages<FollowUpRolledbackCommand>,
            IHandleMessages<FollowUpRolledbackEvent>
        {
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
                        if (scenarioContext.InHandlerTransactionEscalatedToDTC)
                        {
                            scenarioContext.MarkAsFailed(new InvalidOperationException("Transaction was escalated to DTC"));
                        }
                    }

                    scope.Complete();
                }

                throw new Exception("This should NOT prevent the InitiatingMessage from failing.");
            }

            public Task Handle(FollowUpCompletedEvent message, IMessageHandlerContext context)
            {
                scenarioContext.FollowUpCommittedEventReceived = true;
                scenarioContext.MaybeMarkAsCompleted();
                return Task.CompletedTask;
            }

            public Task Handle(FollowUpCompletedCommand completedCommand, IMessageHandlerContext context)
            {
                scenarioContext.FollowUpCommittedCommandReceived = true;
                scenarioContext.MaybeMarkAsCompleted();
                return Task.CompletedTask;
            }

            public Task Handle(FollowUpRolledbackCommand message, IMessageHandlerContext context)
            {
                scenarioContext.FollowUpRolledbackCommandReceived = true;
                scenarioContext.MarkAsFailed(new InvalidOperationException("Message from rolledback transaction should not be received"));
                return Task.CompletedTask;
            }

            public Task Handle(FollowUpRolledbackEvent message, IMessageHandlerContext context)
            {
                scenarioContext.FollowUpRolledbackEventReceived = true;
                scenarioContext.MarkAsFailed(new InvalidOperationException("Message from rolledback transaction should not be received"));
                return Task.CompletedTask;
            }
        }
    }
}