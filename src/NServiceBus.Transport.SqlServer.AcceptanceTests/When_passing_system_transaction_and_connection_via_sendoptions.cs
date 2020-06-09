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
            Guid? distributedTransactionId = null;

            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<AnEndpoint>(c => c.When(async bus =>
                {
                    using (var scope = new System.Transactions.TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                    {
                        using (var connection = new SqlConnection(ConnectionString))
                        {
                            await connection.OpenAsync().ConfigureAwait(false);

                            await bus.Send(new Message());

                            distributedTransactionId = Transaction.Current.TransactionInformation.DistributedIdentifier;
                        }

                        scope.Complete();
                    }
                }))
                .Done(c => c.MessageReceived)
                .Run(TimeSpan.FromMinutes(1));

            Assert.IsFalse(context.MessageReceived);
        }

        class Message : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool MessageReceived { get; set; }
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

            class ReplyHandler : IHandleMessages<Message>
            {
                public MyContext Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    Context.MessageReceived = true;

                    return Task.FromResult(0);
                }
            }
        }


    }
}