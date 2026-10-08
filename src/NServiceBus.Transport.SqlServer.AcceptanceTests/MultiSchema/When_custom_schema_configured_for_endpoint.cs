namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiSchema;

using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

[NonParallelizable] // Subclasses share the nested endpoint types, so they would share queue names.
public abstract class When_custom_schema_configured_for_endpoint : NServiceBusAcceptanceTest
{
    public const string ReceiverSchema = "receiver";

    public class Context : ScenarioContext
    {
        public bool MessageReceived { get; set; }
    }

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() =>
            EndpointSetup<DefaultServer>(c =>
            {
                c.ConfigureSqlServerTransport().DefaultSchema = ReceiverSchema;
            });

        class Handler(Context scenarioContext) : IHandleMessages<Message>
        {
            public Task Handle(Message message, IMessageHandlerContext context)
            {
                scenarioContext.MessageReceived = true;
                scenarioContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class Message : IMessage;
}