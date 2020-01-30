﻿namespace NServiceBus.Transport.SqlServer.AcceptanceTests.NativeTimeouts
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_deferring_a_message_to_the_past_in_native_mode : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_deliver_message()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When((bus, c) =>
                {
                    var options = new SendOptions();

                    options.DoNotDeliverBefore(DateTime.Now.AddHours(-1));
                    options.RouteToThisEndpoint();

                    return bus.Send(new MyMessage(), options);
                }))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.IsTrue(context.MessageReceived);
        }

        public class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(config => config.UseTransport<SqlServerTransport>());
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.MessageReceived = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : IMessage
        {
        }
    }
}