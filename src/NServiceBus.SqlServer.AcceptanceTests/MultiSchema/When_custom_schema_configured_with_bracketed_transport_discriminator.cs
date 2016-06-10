﻿namespace NServiceBus.SqlServer.AcceptanceTests.MultiSchema
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NUnit.Framework;
    using static AcceptanceTesting.Customization.Conventions;

    public class When_custom_schema_configured_with_bracketed_transport_discriminator : When_custom_schema_configured
    {
        [Test]
        public Task Should_receive_message()
        {
            return Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new Message())))
                .WithEndpoint<Receiver>()
                .Done(c => c.MessageReceived)
                .Repeat(r => r.For(Transports.Default))
                .Should(c => Assert.True(c.MessageReceived, "Message should be properly received"))
                .Run();
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var ReceiverName = $"{EndpointNamingConvention(typeof(Receiver))}";

                    c.UnicastRouting().RouteToEndpoint(typeof(Message), $"{ReceiverName}@[{ReceiverSchema}]");
                });
            }
        }
    }
}