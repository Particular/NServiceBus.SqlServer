namespace NServiceBus.SqlServer.AcceptanceTests.MultiSchema
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Configuration.AdvancedExtensibility;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SqlServer;

    public class When_custom_schema_configured_for_legacy_publisher : NServiceBusAcceptanceTest
    {
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
                EndpointSetup<DefaultPublisher>(c =>
                {
                    var transport = c.UseTransport<SqlServerTransport>();
                    transport.DefaultSchema("sender");
                    transport.SubscriptionSettings().SubscriptionTableName("SubscriptionRouting", "dbo");
                    transport.SubscriptionSettings().DisableSubscriptionCache();

                    c.GetSettings().Set("SqlServer.DisableNativePubSub", true);
                    c.OnEndpointSubscribed<Context>((s, context) =>
                    {
                        if (s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(Subscriber))))
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
                EndpointSetup<DefaultServer>(b =>
                {
                    var publisherEndpoint = Conventions.EndpointNamingConvention(typeof(LegacyPublisher));

                    var transport = b.UseTransport<SqlServerTransport>();
                    transport.DefaultSchema("receiver").UseSchemaForEndpoint(publisherEndpoint, "sender");
                    transport.SubscriptionSettings().SubscriptionTableName("SubscriptionRouting", "dbo");

                    transport.EnableMessageDrivenPubSubCompatibilityMode().RegisterPublisher(typeof(Event), Conventions.EndpointNamingConvention(typeof(LegacyPublisher)));
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