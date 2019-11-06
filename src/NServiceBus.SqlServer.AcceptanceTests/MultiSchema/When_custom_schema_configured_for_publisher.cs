namespace NServiceBus.SqlServer.AcceptanceTests.MultiSchema
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQLServer;

    public class When_custom_schema_configured_for_publisher : NServiceBusAcceptanceTest
    {
        [Test]
        public Task Should_receive_event()
        {
            return Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b => b.When(c => c.EndpointsStarted, session => session.Publish(new Event())))
                .WithEndpoint<Subscriber>()
                .Done(c => c.EventReceived)
                .Run();
        }

        class Context : ScenarioContext
        {
            public bool EventReceived { get; set; }
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>(b =>
                {
                    b.UseTransport<SqlServerTransport>()
                        .DefaultSchema("sender")
                        .SubscriptionSettings().SubscriptionTableName("SubscriptionRouting", "dbo");
                });
            }
        }

        class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(b =>
                {
                    var publisherEndpoint = Conventions.EndpointNamingConvention(typeof(Publisher));

                    b.UseTransport<SqlServerTransport>()
                        .DefaultSchema("receiver")
                        .UseSchemaForEndpoint(publisherEndpoint, "sender")
                        .SubscriptionSettings().SubscriptionTableName("SubscriptionRouting", "dbo");

                    // TODO: Use this for compatibility mode
                    //.Routing().RegisterPublisher(
                    //    eventType: typeof(Event),
                    //    publisherEndpoint: publisherEndpoint);
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