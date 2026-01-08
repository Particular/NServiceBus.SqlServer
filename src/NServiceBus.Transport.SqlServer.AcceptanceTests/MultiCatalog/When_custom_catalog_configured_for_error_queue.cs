namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using NUnit.Framework;

public class When_custom_catalog_configured_for_error_queue : MultiCatalogAcceptanceTest
{
    static string SenderConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus1");
    static string SpyConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus2");

    [Test]
    public async Task Error_should_be_sent_to_table_in_configured_catalog()
    {
        var ctx = await Scenario.Define<Context>()
            .WithEndpoint<Sender>(b =>
            {
                b.DoNotFailOnErrorMessages();
                b.When((bus, c) => bus.SendLocal(new Message()));
            })
            .WithEndpoint<ErrorSpy>()
            .Run();

        Assert.That(ctx.FailedMessageProcessed, Is.True, "Message should be moved to error queue in custom schema");
    }

    public class Context : ScenarioContext
    {
        public bool FailedMessageProcessed { get; set; }
    }

    public class Sender : EndpointConfigurationBuilder
    {
        public Sender() =>
            EndpointSetup(new CustomizedServer(SenderConnectionString), (c, sd) =>
            {
                var errorSpyName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ErrorSpy));

                c.SendFailedMessagesTo(errorSpyName);

                c.Recoverability()
                    .Immediate(i => i.NumberOfRetries(0))
                    .Delayed(d => d.NumberOfRetries(0));

                c.ConfigureSqlServerTransport().SchemaAndCatalog.UseCatalogForQueue(errorSpyName, "nservicebus2");
            });

        class Handler : IHandleMessages<Message>
        {
            public Task Handle(Message message, IMessageHandlerContext context) => throw new Exception("Simulated exception");
        }
    }

    public class ErrorSpy : EndpointConfigurationBuilder
    {
        public ErrorSpy() => EndpointSetup(new CustomizedServer(SpyConnectionString), (c, sd) => { });

        class Handler(Context scenarioContext) : IHandleMessages<Message>
        {
            public Task Handle(Message message, IMessageHandlerContext context)
            {
                scenarioContext.FailedMessageProcessed = true;
                scenarioContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class Message : ICommand;
}