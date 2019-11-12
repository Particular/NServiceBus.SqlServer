namespace NServiceBus.SqlServer.AcceptanceTests.MultiSchema
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQLServer;

    public class When_custom_schema_configured_for_publisher_and_subscriber : NServiceBusAcceptanceTest
    {
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
                    transport.DefaultSchema("sender");

                    transport.SubscriptionSettings().SubscriptionTableName("SubscriptionRouting", "dbo");
                    transport.SubscriptionSettings().DisableSubscriptionCache();
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
                    transport.DefaultSchema("receiver");

                    transport.SubscriptionSettings().SubscriptionTableName("SubscriptionRouting", "dbo");
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