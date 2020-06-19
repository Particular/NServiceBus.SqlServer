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

    public class When_passing_system_transaction_and_connection_via_sendoptions : NServiceBusAcceptanceTest
    {
        static string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");

        [Test]
        public async Task Should_use_connection_and_not_escalate_to_DTC()
        {
            Guid? transactionId = null;

            await Scenario.Define<MyContext>()
                .WithEndpoint<AnEndpoint>(c => c.When(async bus =>
                {
                    using (var scope = new System.Transactions.TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                    {
                        using (var connection = new SqlConnection(ConnectionString))
                        {
                            await connection.OpenAsync().ConfigureAwait(false);
                        
                            var sendOptions = new SendOptions();
                            sendOptions.UseCustomSqlConnection(connection);

                            await bus.Send(new Message(), sendOptions);

                            var publishOptions = new PublishOptions();
                            publishOptions.UseCustomSqlConnection(connection);

                            await bus.Publish(new Event(), publishOptions);
                        }

                        transactionId = Transaction.Current.TransactionInformation.DistributedIdentifier;
                        
                        scope.Complete();
                    }
                }))
                .Done(c => c.MessageReceived && c.EventReceived)
                .Run(TimeSpan.FromMinutes(1));

            Assert.AreEqual(Guid.Empty, transactionId);
        }

        class Message : IMessage
        {
        }

        class Event : IEvent
        {
        }

        class MyContext : ScenarioContext
        {
            public bool MessageReceived { get; set; }
            public bool EventReceived { get; set; }
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

                    routing.RouteToEndpoint(typeof(Message), anEndpointName);
                });
            }

            class ReplyHandler : IHandleMessages<Message>, IHandleMessages<Event>
            {
                public MyContext Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    Context.MessageReceived = true;

                    return Task.FromResult(0);
                }

                public Task Handle(Event message, IMessageHandlerContext context)
                {
                    Context.EventReceived = true;

                    return Task.FromResult(0);
                }
            }
        }


    }
}