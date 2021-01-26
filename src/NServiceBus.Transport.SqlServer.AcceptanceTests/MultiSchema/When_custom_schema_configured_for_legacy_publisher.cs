using System;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiSchema
{
    public class When_custom_schema_configured_for_legacy_publisher : NServiceBusAcceptanceTest
    {
        static string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");

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
                EndpointSetup(new CustomizedServer(ConnectionString, false),
                    (c, rd) =>
                    {
                        var transport = c.ConfigureSqlServerTransport();

                        transport.DefaultSchema = "sender";
                        transport.Subscriptions.SubscriptionTableName("SubscriptionRouting", "dbo");
                        transport.Subscriptions.DisableSubscriptionCache();

                        c.OnEndpointSubscribed<Context>((s, context) =>
                        {
                            if (s.SubscriberEndpoint.Contains(
                                AcceptanceTesting.Customization.Conventions
                                    .EndpointNamingConvention(typeof(Subscriber))))
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
                EndpointSetup(new CustomizedServer(ConnectionString), (c, sd) =>
                {
                    var publisherEndpoint =
                        AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(LegacyPublisher));

                    var transport = c.ConfigureSqlServerTransport();
                    transport.DefaultSchema = "receiver";
                    transport.Subscriptions.SubscriptionTableName("SubscriptionRouting", "dbo");

                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode().RegisterPublisher(typeof(Event),
                        AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(LegacyPublisher)));
                    c.ConfigureRouting().UseSchemaForEndpoint(publisherEndpoint, "sender");
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