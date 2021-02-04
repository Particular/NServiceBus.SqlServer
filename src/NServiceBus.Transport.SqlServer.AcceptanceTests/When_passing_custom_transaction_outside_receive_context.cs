namespace NServiceBus.Transport.SqlServer.AcceptanceTests
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_passing_custom_transaction_outside_receive_context : NServiceBusAcceptanceTest
    {
        static string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");

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
                .Done(c => c.SendFromCommittedTransactionReceived && c.PublishFromCommittedTransactionReceived)
                .Run(TimeSpan.FromMinutes(1));

            Assert.IsFalse(context.SendFromRolledbackTransactionReceived);
            Assert.IsFalse(context.PublishFromRolledbackTransactionReceived);
        }

        class CommandFromCommittedTransaction : IMessage
        {
        }

        class CommandFromRolledbackTransaction : IMessage
        {
        }

        class EventFromRollbackedTransaction : IEvent
        {
        }

        class EventFromCommittedTransaction : IEvent
        {
        }

        class MyContext : ScenarioContext
        {
            public bool SendFromCommittedTransactionReceived { get; set; }
            public bool PublishFromCommittedTransactionReceived { get; set; }
            public bool SendFromRolledbackTransactionReceived { get; set; }
            public bool PublishFromRolledbackTransactionReceived { get; set; }
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

                    routing.RouteToEndpoint(typeof(CommandFromCommittedTransaction), anEndpointName);
                    routing.RouteToEndpoint(typeof(CommandFromRolledbackTransaction), anEndpointName);
                });
            }

            class ReplyHandler : IHandleMessages<CommandFromRolledbackTransaction>,
                IHandleMessages<CommandFromCommittedTransaction>,
                IHandleMessages<EventFromRollbackedTransaction>,
                IHandleMessages<EventFromCommittedTransaction>
            {
                readonly MyContext scenarioContext;
                public ReplyHandler(MyContext scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(CommandFromRolledbackTransaction message, IMessageHandlerContext context)
                {
                    scenarioContext.SendFromRolledbackTransactionReceived = true;

                    return Task.CompletedTask;
                }

                public Task Handle(CommandFromCommittedTransaction message, IMessageHandlerContext context)
                {
                    scenarioContext.SendFromCommittedTransactionReceived = true;

                    return Task.CompletedTask;
                }

                public Task Handle(EventFromRollbackedTransaction message, IMessageHandlerContext context)
                {
                    scenarioContext.PublishFromRolledbackTransactionReceived = true;

                    return Task.CompletedTask;
                }

                public Task Handle(EventFromCommittedTransaction message, IMessageHandlerContext context)
                {
                    scenarioContext.PublishFromCommittedTransactionReceived = true;

                    return Task.CompletedTask;
                }
            }
        }
    }
}