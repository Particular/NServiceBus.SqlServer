namespace NServiceBus.SqlServer.AcceptanceTests.MultiSchema
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SqlServer;

    public class When_custom_schema_configured_for_endpoint_with_default_override : When_custom_schema_configured_for_endpoint
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
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<SqlServerTransport>().DefaultSchema(ReceiverSchema).Routing();
                    routing.RouteToEndpoint(typeof(Message), Conventions.EndpointNamingConvention(typeof(Receiver)));
                });
            }
        }
    }
}