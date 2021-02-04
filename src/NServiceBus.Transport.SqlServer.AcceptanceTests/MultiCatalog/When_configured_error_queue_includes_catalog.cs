namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NUnit.Framework;

    public class When_configured_error_queue_includes_catalog : MultiCatalogAcceptanceTest
    {
        [Test]
        public async Task Error_should_be_sent_to_table_in_configured_catalog()
        {
            // makes sure error spy queue is available to avoid race on creating the spy queue
            await Scenario.Define<Context>()
                .WithEndpoint<ErrorSpy>()
                .Done(c => c.EndpointsStarted)
                .Run();

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b =>
                {
                    b.DoNotFailOnErrorMessages();
                    b.When((bus, c) => bus.SendLocal(new Message()));
                })
                .WithEndpoint<ErrorSpy>()
                .Done(c => c.FailedMessageProcessed)
                .Run();

            Assert.True(ctx.FailedMessageProcessed, "Message should be moved to error queue in custom schema");
        }

        public class Context : ScenarioContext
        {
            public bool FailedMessageProcessed { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                var transport = new SqlServerTransport(SenderConnectionString);

                EndpointSetup(new CustomizedServer(transport), (c, rd) =>
                {
                    var errorSpyName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ErrorSpy));

                    c.SendFailedMessagesTo($"{errorSpyName}@[dbo]@[nservicebus2]");

                    c.Recoverability()
                        .Immediate(i => i.NumberOfRetries(0))
                        .Delayed(d => d.NumberOfRetries(0));
                });
            }

            class Handler : IHandleMessages<Message>
            {
                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    throw new Exception("Simulated exception");
                }
            }
        }

        public class ErrorSpy : EndpointConfigurationBuilder
        {
            public ErrorSpy()
            {
                EndpointSetup(new CustomizedServer(SpyConnectionString), (c, sd) => { });
            }

            class Handler : IHandleMessages<Message>
            {
                readonly Context scenarioContext;
                public Handler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    scenarioContext.FailedMessageProcessed = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class Message : ICommand { }

        static string SenderConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus1");
        static string SpyConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus2");
    }
}