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

    public class When_passing_native_transaction_via_options : NServiceBusAcceptanceTest
    {
        static string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");

        [Test]
        public async Task Should_be_used_outside_receivecontext()
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

        [Test]
        public async Task Should_be_used_inside_receivecontext()
        {
            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<AnEndpoint>(c =>
                {
                    c.DoNotFailOnErrorMessages();
                    c.When(async bus => { await bus.SendLocal(new InitiatingMessage()); });
                })
                .Done(c => c.ReceivedMessage)
                .Run(TimeSpan.FromSeconds(10));

            Assert.IsTrue(context.ReceivedMessage);
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

        class InitiateIncomingMessage : IMessage
        class FollowUpMessage : IMessage
        {
        }

        class InitiatingMessage : IMessage
        { }

        class MyContext : ScenarioContext
        {
            public bool ReceivedFromCommittedTransaction { get; set; }
            public bool ReceivedFromRolledbackTransaction { get; set; }
            public bool ReceivedMessage { get; set; }
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
                IHandleMessages<FromCommittedTransaction>
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
            }

            class ImmediateDispatchHandlers : IHandleMessages<FollowUpMessage>, IHandleMessages<InitiatingMessage>
            {
                public MyContext Context { get; set; }

                public Task Handle(FollowUpMessage message, IMessageHandlerContext context)
                {
                    Context.ReceivedMessage = true;
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
