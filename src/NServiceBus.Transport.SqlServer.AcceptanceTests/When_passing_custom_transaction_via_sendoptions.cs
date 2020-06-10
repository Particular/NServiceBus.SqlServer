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

    public class When_passing_native_transaction_via_options : NServiceBusAcceptanceTest
    {
        static string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");

        [Test]
        public async Task Should_be_used_by_send_operations()
        {
            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<AnEndpoint>(c => c.When(async bus =>
                {
                    using (var connection = new SqlConnection(ConnectionString))
                    {
                        connection.Open();

                        using (var rolledbackTransaction = connection.BeginTransaction())
                        {
                            var sendOptions = new SendOptions();
                            sendOptions.UseCustomSqlTransaction(rolledbackTransaction);
                            await bus.Send(new FromRolledbackTransaction(), sendOptions);

                            var publishOptions = new PublishOptions();
                            publishOptions.UseCustomSqlTransaction(rolledbackTransaction);
                            await bus.Publish(new EventFromRollbackedTransaction(), publishOptions);

                            rolledbackTransaction.Rollback();
                        }

                        using (var committedTransaction = connection.BeginTransaction())
                        {
                            var options = new SendOptions();
                            options.UseCustomSqlTransaction(committedTransaction);
                            await bus.Send(new FromCommittedTransaction(), options);

                            var publishOptions = new PublishOptions();
                            publishOptions.UseCustomSqlTransaction(committedTransaction);
                            await bus.Publish(new EventFromCommittedTransaction(), publishOptions);

                            committedTransaction.Commit();
                        }
                    }

                }))
                .Done(c => c.ReceivedFromCommittedTransaction)
                .Run(TimeSpan.FromMinutes(1));

            Assert.IsFalse(context.ReceivedFromRolledbackTransaction);
        }

        class FromCommittedTransaction : IMessage
        {
        }

        class FromRolledbackTransaction : IMessage
        {
        }

        class EventFromRollbackedTransaction : IEvent
        {
        }

        class  EventFromCommittedTransaction : IEvent
        {
        }

        class MyContext : ScenarioContext
        {
            public bool ReceivedFromCommittedTransaction { get; set; }
            public bool ReceivedFromRolledbackTransaction { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.LimitMessageProcessingConcurrencyTo(1);

                    var routing = c.ConfigureTransport().Routing();
                    var anEndpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(AnEndpoint));

                    routing.RouteToEndpoint(typeof(FromCommittedTransaction), anEndpointName);
                    routing.RouteToEndpoint(typeof(FromRolledbackTransaction), anEndpointName);
                });
            }

            class ReplyHandler : IHandleMessages<FromRolledbackTransaction>,
                IHandleMessages<FromCommittedTransaction>,
                IHandleMessages<EventFromRollbackedTransaction>,
                IHandleMessages<EventFromCommittedTransaction>
            {
                public MyContext Context { get; set; }

                public Task Handle(FromRolledbackTransaction message, IMessageHandlerContext context)
                {
                    Context.ReceivedFromRolledbackTransaction = true;

                    return Task.FromResult(0);
                }

                public Task Handle(FromCommittedTransaction message, IMessageHandlerContext context)
                {
                    Context.ReceivedFromCommittedTransaction = true;

                    return Task.FromResult(0);
                }

                public Task Handle(EventFromRollbackedTransaction message, IMessageHandlerContext context)
                {
                    Context.ReceivedFromRolledbackTransaction = true;

                    return Task.CompletedTask;
                }

                public Task Handle(EventFromCommittedTransaction message, IMessageHandlerContext context)
                {
                    Context.ReceivedFromCommittedTransaction = true;

                    return Task.CompletedTask;
                }
            }
        }
    }
}
