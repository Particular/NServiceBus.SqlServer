namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiSchema;

using System.Threading.Tasks;
using AcceptanceTesting;
using Features;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_custom_schema_configured_for_publisher_and_subscriber : NServiceBusAcceptanceTest
{
    [Test]
    public Task Should_receive_event() =>
        Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b => b.When(c => c.Subscribed, session => session.Publish(new Event())))
            .WithEndpoint<Subscriber>(b => b.When(async (s, ctx) =>
            {
                await s.Subscribe(typeof(Event)).ConfigureAwait(false);
                ctx.Subscribed = true;
            }))
            .Run();

    class Context : ScenarioContext
    {
        public bool Subscribed { get; set; }
    }

    class Publisher : EndpointConfigurationBuilder
    {
        public Publisher() =>
            EndpointSetup<DefaultPublisher>(b =>
            {
                var transport = b.ConfigureSqlServerTransport();
                transport.DefaultSchema = "sender";
                transport.Subscriptions.SubscriptionTableName = SubscriptionTableNameCreator.CreateDefault();
                transport.Subscriptions.DisableCaching = true;
            });
    }

    class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber() =>
            EndpointSetup<DefaultServer>(c =>
            {
                var transport = c.ConfigureSqlServerTransport();
                transport.DefaultSchema = "receiver";
                transport.Subscriptions.SubscriptionTableName = SubscriptionTableNameCreator.CreateDefault();

                c.DisableFeature<AutoSubscribe>();
            });

        class EventHandler(Context scenarioContext) : IHandleMessages<Event>
        {
            public Task Handle(Event message, IMessageHandlerContext context)
            {
                scenarioContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class Event : IEvent;
}