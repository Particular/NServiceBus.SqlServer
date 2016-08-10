namespace NServiceBus.SqlServer.AcceptanceTests.MultiSchema
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NUnit.Framework;
    using Routing;
    using static AcceptanceTesting.Customization.Conventions;

    public class When_custom_schema_configured_with_message_mappings : When_custom_schema_configured
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
                EndpointSetup<DefaultServer>((c, r) =>
                {
                    c.UseTransport(r.GetTransportType()).GetSettings().GetOrCreate<UnicastRoutingTable>().RouteToAddress(typeof(Message), $"{EndpointNamingConvention(typeof(Receiver))}@{ReceiverSchema}");
                });
            }
        }
    }
}