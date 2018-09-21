namespace NServiceBus.SqlServer.AcceptanceTests
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQLServer;

    public class When_passing_custom_transaction_via_sendoptions : NServiceBusAcceptanceTest
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
                            var options = new SendOptions();

                            options.UseCustomSqlConnectionAndTransaction(connection, rolledbackTransaction);

                            await bus.Send(new FromRolledbackTransaction(), options);

                            rolledbackTransaction.Rollback();
                        }

                        using (var committedTransaction = connection.BeginTransaction())
                        {
                            var options = new SendOptions();

                            options.UseCustomSqlConnectionAndTransaction(connection, committedTransaction);

                            await bus.Send(new FromCommittedTransaction(), options);

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
        }


    }
}