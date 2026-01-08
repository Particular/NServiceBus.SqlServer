namespace NServiceBus.Transport.SqlServer.AcceptanceTests;

using System;
using System.Threading.Tasks;
using System.Transactions;
using AcceptanceTesting;
using Microsoft.Data.SqlClient;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_passing_custom_transaction_in_receive_context : NServiceBusAcceptanceTest
{
    static readonly string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

    [Test]
    public async Task Should_use_transaction_in_transport_operations()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(c =>
            {
                c.DoNotFailOnErrorMessages();
                c.When(async bus =>
                {
                    await bus.SendLocal(new InitiatingMessage());
                });
            })
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.FollowUpRolledbackCommandReceived, Is.False);
            Assert.That(context.FollowUpRolledbackEventReceived, Is.False);
        }
    }

    class InitiatingMessage : IMessage;

    class FollowUpCommittedCommand : IMessage;

    class FollowUpCommittedEvent : IEvent;

    class FollowUpRolledbackCommand : IMessage;

    class FollowUpRolledbackEvent : IEvent;

    class Context : ScenarioContext
    {
        public bool FollowUpCommittedCommandReceived { get; set; }
        public bool FollowUpCommittedEventReceived { get; set; }
        public bool FollowUpRolledbackCommandReceived { get; set; }
        public bool FollowUpRolledbackEventReceived { get; set; }

        public void MaybeMarkAsCompleted() => MarkAsCompleted(FollowUpCommittedCommandReceived, FollowUpCommittedEventReceived);
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint() =>
            EndpointSetup<DefaultServer>(c =>
            {
                c.LimitMessageProcessingConcurrencyTo(1);
            });

        class ImmediateDispatchHandlers(Context scenarioContext) : IHandleMessages<InitiatingMessage>,
            IHandleMessages<FollowUpCommittedCommand>,
            IHandleMessages<FollowUpCommittedEvent>,
            IHandleMessages<FollowUpRolledbackCommand>,
            IHandleMessages<FollowUpRolledbackEvent>
        {
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
                scenarioContext.MaybeMarkAsCompleted();
                return Task.CompletedTask;
            }

            public Task Handle(FollowUpCommittedCommand message, IMessageHandlerContext context)
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