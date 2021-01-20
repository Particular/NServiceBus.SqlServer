namespace NServiceBus.Transport.SqlServer.AcceptanceTests.NativeTimeouts
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_deferring_a_message_in_native_mode : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_delay_delivery()
        {
            var delay = TimeSpan.FromMilliseconds(1);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When((session, c) =>
                {
                    var options = new SendOptions();

                    options.DelayDeliveryWith(delay);
                    options.RouteToThisEndpoint();

                    c.SentAt = DateTimeOffset.UtcNow;

                    return session.Send(new MyMessage(), options);
                }))
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

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly Context scenarioContext;
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

        public class MyMessage : IMessage
        {
        }
    }
}