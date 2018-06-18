namespace NServiceBus.SqlServer.AcceptanceTests
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Extensibility;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport;
    using Transport.SQLServer;

    public class When_passing_custom_transaction_via_sendoptions : NServiceBusAcceptanceTest
    {
        public static string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransport.ConnectionString");

        [Test]
        public async Task Should_be_used_by_send_operations()
        {
            await Scenario.Define<MyContext>()
                .WithEndpoint<AnEndpoint>(c => c.When(async bus =>
                {
                    using (var connection = new SqlConnection(@"Server=localhost\sqlexpress;Initial Catalog=nservicebus;uid=sa;pwd=sa;"))
                    {
                        connection.Open();

                        using (var rolledbackTrasaction = connection.BeginTransaction())
                        {
                            var options = new SendOptions();
                            options.RouteToThisEndpoint();

                            options.UseCustomSqlConnectionAndTransaction(connection, rolledbackTrasaction);

                            await bus.Send(new Request{FromCommitedTransaction = false}, options);

                            rolledbackTrasaction.Rollback();
                        }

                        using (var commitedTransaction = connection.BeginTransaction())
                        {
                            var options = new SendOptions();
                            options.RouteToThisEndpoint();

                            options.UseCustomSqlConnectionAndTransaction(connection, commitedTransaction);

                            await bus.Send(new Request{FromCommitedTransaction = true}, options);

                            commitedTransaction.Commit();
                        }
                    }
                    
                }))
                .Done(c => c.Done)
                .Run(TimeSpan.FromMinutes(1));
        }

        public class Request : IMessage
        {
            public bool FromCommitedTransaction { get; set; }
        }

        class MyContext : ScenarioContext
        {
            public bool Done { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            class ReplyHandler : IHandleMessages<Request>
            {
                public MyContext Context { get; set; }

                public Task Handle(Request message, IMessageHandlerContext context)
                {
                    Context.Done = message.FromCommitedTransaction;

                    return Task.FromResult(0);
                }
            }
        }

       
    }
}