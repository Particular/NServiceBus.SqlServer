namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Features;
    using NUnit.Framework;
    using Transport.SQLServer;

    public class When_subscribing_natively_in_compatibility_mode : NServiceBusAcceptanceTest
    {
        [Test]
        public void If_event_is_not_marked_endpoint_does_not_start()
        {
            var exception = Assert.ThrowsAsync<Exception>(async () => await Scenario.Define<Context>()
                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c => { c.ConfigureSqlServerTransport().EnableMessageDrivenPubSubCompatibilityMode(); });
                    b.When(async (session, ctx) =>
                    {
                        await session.Subscribe<MyEvent>();
                    });
                })
                .Done(c => c.EndpointsStarted)
                .Run());

            StringAssert.Contains("When an endpoint is set to message-driven pub/sub compatibility mode, all subscribed events need to be configured", exception.Message);
        }

        [Test]
        public async Task Natively_subscribed_events_can_be_marked_by_type()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var compatMode = c.ConfigureSqlServerTransport().EnableMessageDrivenPubSubCompatibilityMode();
                        compatMode.SubscribeNatively(typeof(MyEvent));
                    });
                    b.When(async (session, ctx) => { await session.Subscribe<MyEvent>(); });
                })
                .Done(c => c.EndpointsStarted)
                .Run();
        }

        [Test]
        public async Task Natively_subscribed_events_can_be_marked_by_assembly()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var compatMode = c.ConfigureSqlServerTransport().EnableMessageDrivenPubSubCompatibilityMode();
                        compatMode.SubscribeNatively(typeof(MyEvent).Assembly);
                    });
                    b.When(async (session, ctx) => { await session.Subscribe<MyEvent>(); });
                })
                .Done(c => c.EndpointsStarted)
                .Run();
        }

        [Test]
        public async Task Natively_subscribed_events_can_be_marked_by_assembly_and_namespace()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var compatMode = c.ConfigureSqlServerTransport().EnableMessageDrivenPubSubCompatibilityMode();
                        compatMode.SubscribeNatively(typeof(MyEvent).Assembly, typeof(MyEvent).Namespace);
                    });
                    b.When(async (session, ctx) => { await session.Subscribe<MyEvent>(); });
                })
                .Done(c => c.EndpointsStarted)
                .Run();
        }

        public class Context : ScenarioContext
        {
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c => { c.DisableFeature<AutoSubscribe>(); });
            }

            public class MyEventHandler : IHandleMessages<MyEvent>
            {
                public Context Context { get; set; }

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }

        public class MyEvent : IEvent
        {
        }
    }
}