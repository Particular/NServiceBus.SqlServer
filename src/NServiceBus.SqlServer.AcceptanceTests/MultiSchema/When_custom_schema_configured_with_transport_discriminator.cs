namespace NServiceBus.SqlServer.AcceptanceTests.MultiSchema
{
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NUnit.Framework;
    using static AcceptanceTesting.Customization.Conventions;

    public class When_custom_schema_configured_with_transport_discriminator : When_custom_schema_configured
    {
        [Test]
        public async void Should_receive_message()
        {
            await Scenario.Define<Context>()
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
                    
                    c.Routing().UnicastRoutingTable.RouteToEndpoint(typeof(Message), $"{ReceiverName}@{ReceiverSchema}");
                });
            }
        }
    }
}