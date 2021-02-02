namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_custom_catalog_configured_for_legacy_publisher : MultiCatalogAcceptanceTest
    {
        static string PublisherConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus1");
        static string SubscriberConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus2");

        static string PublisherEndpoint =>
            AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(LegacyPublisher));

        [Test]
        public Task Should_receive_event()
        {
            return Scenario.Define<Context>()
                .WithEndpoint<LegacyPublisher>(b => b.When(c => c.Subscribed, session => session.Publish(new Event())))
                .WithEndpoint<Subscriber>(b => b.When(c => c.EndpointsStarted, s => s.Subscribe(typeof(Event))))
                .Done(c => c.EventReceived)
                .Run();
        }

        class Context : ScenarioContext
        {
            public bool EventReceived { get; set; }
            public bool Subscribed { get; set; }
        }

        class LegacyPublisher : EndpointConfigurationBuilder
        {
            public LegacyPublisher()
            {
                EndpointSetup(new CustomizedServer(PublisherConnectionString, false),
                    (c, rd) =>
                    {
                        var transport = c.ConfigureSqlServerTransport();
                        transport.Subscriptions.SubscriptionTableName = new SubscriptionTableName("SubscriptionRouting", "dbo", "nservicebus");
                        transport.Subscriptions.DisableCaching = true;

                        c.OnEndpointSubscribed<Context>((s, context) =>
                        {
                            var subscriberName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Subscriber));
                            if (s.SubscriberEndpoint.Contains(subscriberName))
                            {
                                context.Subscribed = true;
                            }
                        });
                    });
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
                    var routing = c.ConfigureRouting();
                    routing.EnableMessageDrivenPubSubCompatibilityMode()
                        .RegisterPublisher(typeof(Event), PublisherEndpoint);
                    routing.UseCatalogForEndpoint(PublisherEndpoint, "nservicebus1");
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