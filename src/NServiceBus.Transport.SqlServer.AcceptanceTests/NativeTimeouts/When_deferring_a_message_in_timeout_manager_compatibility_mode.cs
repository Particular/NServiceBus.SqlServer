namespace NServiceBus.Transport.SqlServer.AcceptanceTests.NativeTimeouts
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Features;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_deferring_a_message_in_timeout_manager_compatibility_mode : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_delay_delivery()
        {
            var delay = TimeSpan.FromSeconds(5);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<SenderEndpoint>(b => b.When((session, c) =>
                {
                    var options = new SendOptions();

                    options.SetHeader("NServiceBus.Timeout.RouteExpiredTimeoutTo", Conventions.EndpointNamingConvention(typeof(SenderEndpoint)));
                    options.SetHeader("NServiceBus.Timeout.Expire", DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow + delay));
                    options.SetDestination(Conventions.EndpointNamingConvention(typeof(CompatibilityModeEndpoint)) + ".Timeouts");

                    c.SentAt = DateTimeOffset.UtcNow;

                    return session.Send(new MyMessage(), options);
                }))
                .WithEndpoint<CompatibilityModeEndpoint>()
                .Done(c => c.WasCalled)
                .Run();

            Assert.GreaterOrEqual(context.ReceivedAt - context.SentAt, delay);
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
            public DateTimeOffset SentAt { get; set; }
            public DateTimeOffset ReceivedAt { get; set; }
        }

        public class SenderEndpoint : EndpointConfigurationBuilder
        {
            public SenderEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                private readonly Context scenarioContext;
                public MyMessageHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.ReceivedAt = DateTimeOffset.UtcNow;
                    scenarioContext.WasCalled = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class CompatibilityModeEndpoint : EndpointConfigurationBuilder
        {
            public CompatibilityModeEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableFeature<TimeoutManager>(); //Because the acceptance tests framework disables it by default.
                    c.UseTransport<SqlServerTransport>().NativeDelayedDelivery().EnableTimeoutManagerCompatibility();
                });
            }
        }

        public class MyMessage : IMessage
        {
        }
    }
}