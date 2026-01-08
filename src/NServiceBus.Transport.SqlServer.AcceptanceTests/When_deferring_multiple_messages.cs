namespace NServiceBus.AcceptanceTests.DelayedDelivery;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using NUnit.Framework;

public class When_deferring_multiple_messages : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Adaptive_polling_should_work()
    {
        var delay = TimeSpan.FromSeconds(2);
        var longDelay = TimeSpan.FromDays(1);

        var context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b => b.When(async (session, c) =>
            {
                var longOptions = new SendOptions();

                longOptions.DelayDeliveryWith(longDelay);
                longOptions.RouteToThisEndpoint();

                await session.Send(new MyMessage { Which = "Long" }, longOptions);

                var options = new SendOptions();

                options.DelayDeliveryWith(delay);
                options.RouteToThisEndpoint();

                await session.Send(new MyMessage { Which = "Short" }, options);
            }))
            .Run();

        Assert.That(context.WhichWasCalled, Is.EqualTo("Short"));
    }

    public class Context : ScenarioContext
    {
        public string WhichWasCalled { get; set; }
    }

    public class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() => EndpointSetup<DefaultServer>();

        public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                testContext.WhichWasCalled = message.Which;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyMessage : IMessage
    {
        public string Which { get; set; }
    }
}