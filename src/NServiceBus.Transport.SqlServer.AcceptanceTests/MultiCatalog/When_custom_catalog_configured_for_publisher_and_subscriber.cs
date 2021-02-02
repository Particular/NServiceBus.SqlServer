namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using NUnit.Framework;

    public class When_custom_catalog_configured_for_publisher_and_subscriber : MultiCatalogAcceptanceTest
    {
        static string PublisherConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus1");
        static string SubscriberConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus2");

        [Test]
        public Task Should_receive_event()
        {
            return Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b => b.When(c => c.Subscribed, session => session.Publish(new Event())))
                .WithEndpoint<Subscriber>(b => b.When(c => c.EndpointsStarted, async (s, ctx) =>
                {
                    await s.Subscribe(typeof(Event)).ConfigureAwait(false);
                    ctx.Subscribed = true;
                }))
                .Done(c => c.EventReceived)
                .Run();
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

            class EventHandler : IHandleMessages<Event>
            {
                readonly Context scenarioContext;
                public EventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(Event message, IMessageHandlerContext context)
                {
                    scenarioContext.EventReceived = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class Event : IEvent
        {
        }
    }
}