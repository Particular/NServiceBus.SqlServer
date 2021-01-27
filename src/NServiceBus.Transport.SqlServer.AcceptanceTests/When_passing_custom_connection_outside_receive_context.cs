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

    public class When_passing_custom_connection_outside_receive_context : NServiceBusAcceptanceTest
    {
        static string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");

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
                .Done(c => c.MarkerMessageReceived)
                .Run(TimeSpan.FromMinutes(1));

            Assert.IsFalse(context.SendFromRollbackedScopeReceived);
            Assert.IsFalse(context.PublishFromRollbackedScopeReceived);
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
                .Done(c => c.SendFromCompletedScopeReceived && c.PublishFromCompletedScopeReceived)
                .Run(TimeSpan.FromMinutes(1));

            Assert.AreEqual(Guid.Empty, transactionId);
        }

        class MarkerMessage : IMessage
        {
        }

        class CommandFromCompletedScope : IMessage
        {
        }

        class EventFromCompletedScope : IEvent
        {
        }

        class CommandFromRollbackedScope : IMessage
        {
        }

        class EventFromRollbackedScope : IEvent
        {
        }

        class MyContext : ScenarioContext
        {
            public bool MarkerMessageReceived { get; set; }
            public bool SendFromCompletedScopeReceived { get; set; }
            public bool PublishFromCompletedScopeReceived { get; set; }
            public bool SendFromRollbackedScopeReceived { get; set; }
            public bool PublishFromRollbackedScopeReceived { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.LimitMessageProcessingConcurrencyTo(1);

                    var routing = c.ConfigureRouting();
                    var anEndpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(AnEndpoint));

                    routing.RouteToEndpoint(typeof(CommandFromCompletedScope), anEndpointName);
                    routing.RouteToEndpoint(typeof(CommandFromRollbackedScope), anEndpointName);
                });
            }

            class ReplyHandler : IHandleMessages<CommandFromCompletedScope>,
                IHandleMessages<EventFromCompletedScope>,
                IHandleMessages<CommandFromRollbackedScope>,
                IHandleMessages<EventFromRollbackedScope>,
                IHandleMessages<MarkerMessage>
            {
                readonly MyContext scenarioContext;
                public ReplyHandler(MyContext scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(CommandFromCompletedScope commandFromCompletedScope, IMessageHandlerContext context)
                {
                    scenarioContext.SendFromCompletedScopeReceived = true;

                    return Task.CompletedTask;
                }

                public Task Handle(EventFromCompletedScope message, IMessageHandlerContext context)
                {
                    scenarioContext.PublishFromCompletedScopeReceived = true;

                    return Task.CompletedTask;
                }

                public Task Handle(CommandFromRollbackedScope message, IMessageHandlerContext context)
                {
                    scenarioContext.SendFromRollbackedScopeReceived = true;

                    return Task.CompletedTask;
                }

                public Task Handle(EventFromRollbackedScope message, IMessageHandlerContext context)
                {
                    scenarioContext.PublishFromRollbackedScopeReceived = true;

                    return Task.CompletedTask;
                }

                public Task Handle(MarkerMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.MarkerMessageReceived = true;

                    return Task.CompletedTask;
                }
            }
        }
    }
}