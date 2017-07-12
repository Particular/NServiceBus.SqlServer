namespace NServiceBus.AcceptanceTests.NativeTimeouts
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using Features;
    using NUnit.Framework;
    using Transport.SQLServer;

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
                    options.SetHeader("NServiceBus.Timeout.Expire", DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow + delay));

                    options.SetDestination(Conventions.EndpointNamingConvention(typeof(CompatibilityModeEndpoint)) + ".Timeouts");

                    c.SentAt = DateTime.UtcNow;

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
            public DateTime SentAt { get; set; }
            public DateTime ReceivedAt { get; set; }
        }

        public class SenderEndpoint : EndpointConfigurationBuilder
        {
            public SenderEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.ReceivedAt = DateTime.UtcNow;
                    Context.WasCalled = true;
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
                    c.UseTransport<SqlServerTransport>().UseNativeDelayedDelivery();
                });
            }
        }


        public class MyMessage : IMessage
        {
        }
    }
}