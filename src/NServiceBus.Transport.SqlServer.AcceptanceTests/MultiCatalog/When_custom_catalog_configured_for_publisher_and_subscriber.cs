namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using SqlServer;

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
                EndpointSetup<DefaultPublisher>(b =>
                {
                    var transport = b.UseTransport<SqlServerTransport>();
                    transport.ConnectionString(PublisherConnectionString);

                    transport.SubscriptionSettings().DisableSubscriptionCache();
                    transport.SubscriptionSettings().SubscriptionTableName("SubscriptionRouting", "dbo", "nservicebus");
                });
            }
        }

        class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var transport = c.UseTransport<SqlServerTransport>();

                    transport.ConnectionString(SubscriberConnectionString);
                    transport.SubscriptionSettings().SubscriptionTableName("SubscriptionRouting", "dbo", "nservicebus");

                    c.DisableFeature<AutoSubscribe>();
                });
            }

            class EventHandler : IHandleMessages<Event>
            {
                public Context Context { get; set; }

                public Task Handle(Event message, IMessageHandlerContext context)
                {
                    Context.EventReceived = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class Event : IEvent
        {
        }
    }
}