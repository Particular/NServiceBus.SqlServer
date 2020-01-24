namespace NServiceBus.AcceptanceTests.NativeTimeouts
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;
    using Transport.SqlServer;

    public class When_using_TTBR_for_deferred_message_in_native_mode : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw()
        {
            var exception = Assert.ThrowsAsync<Exception>(async() => await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When((session, c) =>
                {
                    var options = new SendOptions();
                    options.DelayDeliveryWith(TimeSpan.FromSeconds(5));
                    options.RouteToThisEndpoint();

                    return session.Send(new MyMessage(), options);
                }))
                .Done(c => c.WasCalled)
                .Run());

            StringAssert.Contains("Delayed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to delay messages of this type.", exception.Message);
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(config => config.UseTransport<SqlServerTransport>().NativeDelayedDelivery());
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.WasCalled = true;
                    return Task.FromResult(0);
                }
            }
        }

        [TimeToBeReceived("00:00:10")]
        public class MyMessage : IMessage
        {
        }
    }
}