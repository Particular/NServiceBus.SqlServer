namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiSchema
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Configuration.AdvancedExtensibility;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Routing;
    using static AcceptanceTesting.Customization.Conventions;

    //HINT: Message mappings specified in app.config added to routing table using UnicastRoute.CreateFromPhysicalAddress.
    //      As a result this test covers also an app.config message mappings scenario
    public class When_custom_schema_configured_for_endpoint_inside_physical_address : When_custom_schema_configured_for_endpoint
    {
        [Test]
        public async Task Should_receive_message()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new Message())))
                .WithEndpoint<Receiver>()
                .Done(c => c.MessageReceived)
                .Run();

            Assert.True(ctx.MessageReceived, "Message should be properly received");
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>((c, r) =>
                {
                    var receiverAddress = $"{EndpointNamingConvention(typeof(Receiver))}@{ReceiverSchema}";

                    var transportSettings = c.ConfigureSqlServerTransport();

                    c.GetSettings()
                    .GetOrCreate<UnicastRoutingTable>()
                    .AddOrReplaceRoutes("Custom", new List<RouteTableEntry>
                    {
                        new RouteTableEntry(typeof(Message), UnicastRoute.CreateFromPhysicalAddress(receiverAddress))
                    });
                });
            }
        }
    }
}