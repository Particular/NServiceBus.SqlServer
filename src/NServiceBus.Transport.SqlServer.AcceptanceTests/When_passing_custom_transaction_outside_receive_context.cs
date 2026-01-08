namespace NServiceBus.Transport.SqlServer.AcceptanceTests;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using Microsoft.Data.SqlClient;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_passing_custom_transaction_outside_receive_context : NServiceBusAcceptanceTest
{
    static readonly string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

    [Test]
    public async Task Should_use_transaction_in_transport_operations()
    {
        var context = await Scenario.Define<MyContext>()
            .WithEndpoint<AnEndpoint>(c => c.When(async bus =>
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();

                    SendOptions sendOptions;
                    PublishOptions publishOptions;

                    using (var rolledbackTransaction = connection.BeginTransaction())
                    {
                        sendOptions = new SendOptions();
                        sendOptions.UseCustomSqlTransaction(rolledbackTransaction);
                        await bus.Send(new CommandFromRolledbackTransaction(), sendOptions);

                        publishOptions = new PublishOptions();
                        publishOptions.UseCustomSqlTransaction(rolledbackTransaction);
                        await bus.Publish(new EventFromRollbackedTransaction(), publishOptions);

                        rolledbackTransaction.Rollback();
                    }

                    using (var committedTransaction = connection.BeginTransaction())
                    {
                        sendOptions = new SendOptions();
                        sendOptions.UseCustomSqlTransaction(committedTransaction);
                        await bus.Send(new CommandFromCommittedTransaction(), sendOptions);

                        publishOptions = new PublishOptions();
                        publishOptions.UseCustomSqlTransaction(committedTransaction);
                        await bus.Publish(new EventFromCommittedTransaction(), publishOptions);

                        committedTransaction.Commit();
                    }
                }

            }))
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(context.SendFromRolledbackTransactionReceived, Is.False);
            Assert.That(context.PublishFromRolledbackTransactionReceived, Is.False);
        });
    }

    class CommandFromCommittedTransaction : IMessage;

    class CommandFromRolledbackTransaction : IMessage;

    class EventFromRollbackedTransaction : IEvent;

    class EventFromCommittedTransaction : IEvent;

    class MyContext : ScenarioContext
    {
        public bool SendFromCommittedTransactionReceived { get; set; }
        public bool PublishFromCommittedTransactionReceived { get; set; }
        public bool SendFromRolledbackTransactionReceived { get; set; }
        public bool PublishFromRolledbackTransactionReceived { get; set; }

        public void MaybeMarkAsCompleted() => MarkAsCompleted(SendFromCommittedTransactionReceived && PublishFromCommittedTransactionReceived);
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint() =>
            EndpointSetup<DefaultServer>(c =>
            {
                c.LimitMessageProcessingConcurrencyTo(1);

                var routing = c.ConfigureRouting();
                var anEndpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(AnEndpoint));

                routing.RouteToEndpoint(typeof(CommandFromCommittedTransaction), anEndpointName);
                routing.RouteToEndpoint(typeof(CommandFromRolledbackTransaction), anEndpointName);
            });

        class ReplyHandler(MyContext scenarioContext) : IHandleMessages<CommandFromRolledbackTransaction>,
            IHandleMessages<CommandFromCommittedTransaction>,
            IHandleMessages<EventFromRollbackedTransaction>,
            IHandleMessages<EventFromCommittedTransaction>
        {
            public Task Handle(CommandFromRolledbackTransaction message, IMessageHandlerContext context)
            {
                scenarioContext.SendFromRolledbackTransactionReceived = true;
                scenarioContext.MarkAsFailed(new InvalidOperationException("Message from rolledback transaction should not be received"));
                return Task.CompletedTask;
            }

            public Task Handle(CommandFromCommittedTransaction message, IMessageHandlerContext context)
            {
                scenarioContext.SendFromCommittedTransactionReceived = true;
                scenarioContext.MaybeMarkAsCompleted();
                return Task.CompletedTask;
            }

            public Task Handle(EventFromRollbackedTransaction message, IMessageHandlerContext context)
            {
                scenarioContext.PublishFromRolledbackTransactionReceived = true;
                scenarioContext.MarkAsFailed(new InvalidOperationException("Message from rolledback transaction should not be received"));
                return Task.CompletedTask;
            }

            public Task Handle(EventFromCommittedTransaction message, IMessageHandlerContext context)
            {
                scenarioContext.PublishFromCommittedTransactionReceived = true;
                scenarioContext.MaybeMarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }
}