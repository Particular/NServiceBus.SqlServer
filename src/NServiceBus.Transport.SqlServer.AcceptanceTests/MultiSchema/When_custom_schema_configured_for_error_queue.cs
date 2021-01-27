namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiSchema
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_custom_schema_configured_for_error_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Error_should_be_sent_to_table_in_configured_schema()
        {
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
                EndpointSetup<DefaultServer>(c =>
                {
                    var errorSpyName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ErrorSpy));

                    c.SendFailedMessagesTo(errorSpyName);

                    c.Recoverability()
                        .Immediate(i => i.NumberOfRetries(0))
                        .Delayed(d => d.NumberOfRetries(0));

                    var transport = c.ConfigureSqlServerTransport();
                    transport.SchemaAndCatalog.UseSchemaForQueue(errorSpyName, ErrorSpySchema);
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
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureSqlServerTransport().DefaultSchema = ErrorSpySchema;
                });
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

        const string ErrorSpySchema = "receiver";
    }
}