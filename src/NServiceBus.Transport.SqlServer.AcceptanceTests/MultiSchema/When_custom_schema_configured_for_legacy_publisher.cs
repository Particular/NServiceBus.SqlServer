﻿using System;

namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiSchema
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

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
                EndpointSetup<DefaultPublisher>(c =>
                {
                    var transport = new SqlServerTransport(supportsPublishSubscribe: false);
                    transport.ConnectionString = ConnectionString;
                    transport.DefaultSchema = "sender";
                    transport.Subscriptions.SubscriptionTableName("SubscriptionRouting", "dbo");
                    transport.Subscriptions.DisableSubscriptionCache();

                    c.UseTransport(transport);

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

                    var transport = new SqlServerTransport();
                    transport.ConnectionString = ConnectionString;
                    transport.DefaultSchema = "receiver";
                    transport.Subscriptions.SubscriptionTableName("SubscriptionRouting", "dbo");
                    b.UseTransport(transport);

                    b.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode().RegisterPublisher(typeof(Event), Conventions.EndpointNamingConvention(typeof(LegacyPublisher)));
                    b.ConfigureRouting().UseSchemaForEndpoint(publisherEndpoint, "sender");
                });
            }

            class EventHandler : IHandleMessages<Event>
            {
                private readonly Context scenarioContext;
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