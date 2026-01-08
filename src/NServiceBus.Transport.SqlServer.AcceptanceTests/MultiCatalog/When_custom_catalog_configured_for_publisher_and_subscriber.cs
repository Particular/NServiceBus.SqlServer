namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog;

using System.Threading.Tasks;
using AcceptanceTesting;
using Features;
using NUnit.Framework;

public class When_custom_catalog_configured_for_publisher_and_subscriber : MultiCatalogAcceptanceTest
{
    static string PublisherConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus1");
    static string SubscriberConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus2");

    [Test]
    public async Task Should_receive_event()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b => b.When(c => c.Subscribed, session => session.Publish(new Event())))
            .WithEndpoint<Subscriber>(b => b.When(async (s, ctx) =>
            {
                await s.Subscribe(typeof(Event)).ConfigureAwait(false);
                ctx.Subscribed = true;
            }))
            .Run();

        Assert.That(context.EventReceived, Is.True);
    }

    class Context : ScenarioContext
    {
        public bool EventReceived { get; set; }
        public bool Subscribed { get; set; }
    }

    class Publisher : EndpointConfigurationBuilder
    {
        public Publisher()
        {
            var transport = new SqlServerTransport(PublisherConnectionString);
            transport.Subscriptions.DisableCaching = true;
            transport.Subscriptions.SubscriptionTableName = new SubscriptionTableName("SubscriptionRouting", "dbo", "nservicebus");

            EndpointSetup(new CustomizedServer(transport), (c, rd) => { });
        }
    }

    class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber()
        {
            var transport = new SqlServerTransport(SubscriberConnectionString);
            transport.Subscriptions.SubscriptionTableName = new SubscriptionTableName("SubscriptionRouting", "dbo", "nservicebus");

            EndpointSetup(new CustomizedServer(transport), (c, rd) =>
            {
                c.DisableFeature<AutoSubscribe>();
            });
        }

        class EventHandler(Context scenarioContext) : IHandleMessages<Event>
        {
            public Task Handle(Event message, IMessageHandlerContext context)
            {
                scenarioContext.EventReceived = true;
                scenarioContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class Event : IEvent
    {
    }
}