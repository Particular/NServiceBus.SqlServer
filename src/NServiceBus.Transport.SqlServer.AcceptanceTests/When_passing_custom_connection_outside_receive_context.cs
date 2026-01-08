namespace NServiceBus.Transport.SqlServer.AcceptanceTests;

using System;
using System.Threading.Tasks;
using System.Transactions;
using AcceptanceTesting;
using Microsoft.Data.SqlClient;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_passing_custom_connection_outside_receive_context : NServiceBusAcceptanceTest
{
    static readonly string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

    [Test]
    public async Task Should_use_connection_for_all_transport_operations()
    {
        var context = await Scenario.Define<MyContext>()
            .WithEndpoint<AnEndpoint>(c => c.When(async bus =>
            {
                //HINT: this scope is never committed
                using (new System.Transactions.TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    using (var connection = new SqlConnection(ConnectionString))
                    {
                        await connection.OpenAsync().ConfigureAwait(false);

                        var sendOptions = new SendOptions();
                        sendOptions.UseCustomSqlConnection(connection);
                        await bus.Send(new CommandFromRollbackedScope(), sendOptions);

                        var publishOptions = new PublishOptions();
                        publishOptions.UseCustomSqlConnection(connection);
                        await bus.Publish(new EventFromRollbackedScope(), publishOptions);
                    }
                }

                await bus.SendLocal(new MarkerMessage());
            }))
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.MarkerMessageReceived, Is.True);
            Assert.That(context.SendFromRollbackedScopeReceived, Is.False);
            Assert.That(context.PublishFromRollbackedScopeReceived, Is.False);
        }
    }

    [Test]
    public async Task Should_use_connection_and_not_escalate_to_DTC()
    {
        Guid? transactionId = null;

        await Scenario.Define<MyContext>()
            .WithEndpoint<AnEndpoint>(c => c.When(async bus =>
            {
                using (var completedScope = new System.Transactions.TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    using (var connection = new SqlConnection(ConnectionString))
                    {
                        await connection.OpenAsync().ConfigureAwait(false);

                        var sendOptions = new SendOptions();
                        sendOptions.UseCustomSqlConnection(connection);
                        await bus.Send(new CommandFromCompletedScope(), sendOptions);

                        var publishOptions = new PublishOptions();
                        publishOptions.UseCustomSqlConnection(connection);
                        await bus.Publish(new EventFromCompletedScope(), publishOptions);
                    }

                    transactionId = Transaction.Current.TransactionInformation.DistributedIdentifier;

                    completedScope.Complete();
                }
            }))
            .Run();

        Assert.That(transactionId, Is.EqualTo(Guid.Empty));
    }

    class MarkerMessage : IMessage;

    class CommandFromCompletedScope : IMessage;

    class EventFromCompletedScope : IEvent;

    class CommandFromRollbackedScope : IMessage;

    class EventFromRollbackedScope : IEvent;

    class MyContext : ScenarioContext
    {
        public bool MarkerMessageReceived { get; set; }
        public bool SendFromCompletedScopeReceived { get; set; }
        public bool PublishFromCompletedScopeReceived { get; set; }
        public bool SendFromRollbackedScopeReceived { get; set; }
        public bool PublishFromRollbackedScopeReceived { get; set; }

        public void MaybeMarkAsCompleted() => MarkAsCompleted(SendFromCompletedScopeReceived, PublishFromCompletedScopeReceived);
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint() =>
            EndpointSetup<DefaultServer>(c =>
            {
                c.LimitMessageProcessingConcurrencyTo(1);

                var routing = c.ConfigureRouting();
                var anEndpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(AnEndpoint));

                routing.RouteToEndpoint(typeof(CommandFromCompletedScope), anEndpointName);
                routing.RouteToEndpoint(typeof(CommandFromRollbackedScope), anEndpointName);
            });

        class ReplyHandler(MyContext scenarioContext) : IHandleMessages<CommandFromCompletedScope>,
            IHandleMessages<EventFromCompletedScope>,
            IHandleMessages<CommandFromRollbackedScope>,
            IHandleMessages<EventFromRollbackedScope>,
            IHandleMessages<MarkerMessage>
        {
            public Task Handle(CommandFromCompletedScope commandFromCompletedScope, IMessageHandlerContext context)
            {
                scenarioContext.SendFromCompletedScopeReceived = true;
                scenarioContext.MaybeMarkAsCompleted();
                return Task.CompletedTask;
            }

            public Task Handle(EventFromCompletedScope message, IMessageHandlerContext context)
            {
                scenarioContext.PublishFromCompletedScopeReceived = true;
                scenarioContext.MaybeMarkAsCompleted();
                return Task.CompletedTask;
            }

            public Task Handle(CommandFromRollbackedScope message, IMessageHandlerContext context)
            {
                scenarioContext.SendFromRollbackedScopeReceived = true;
                scenarioContext.MarkAsFailed(new InvalidOperationException("Message from rolledback transaction should not be received"));
                return Task.CompletedTask;
            }

            public Task Handle(EventFromRollbackedScope message, IMessageHandlerContext context)
            {
                scenarioContext.PublishFromRollbackedScopeReceived = true;
                scenarioContext.MarkAsFailed(new InvalidOperationException("Message from rolledback transaction should not be received"));
                return Task.CompletedTask;
            }

            public Task Handle(MarkerMessage message, IMessageHandlerContext context)
            {
                scenarioContext.MarkerMessageReceived = true;
                scenarioContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }
}