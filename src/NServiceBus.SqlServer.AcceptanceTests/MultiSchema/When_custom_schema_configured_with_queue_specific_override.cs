namespace NServiceBus.SqlServer.AcceptanceTests.MultiSchema
{
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;
    using static AcceptanceTesting.Customization.Conventions;

    public class When_custom_schem_configured_with_queue_specific_override : When_custom_schema_configured
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
                    c.UseTransport<SqlServerTransport>()
                     .UseSpecificSchema(tn => tn == EndpointNamingConvention(typeof(Receiver)) ? ReceiverSchema : null);
                }).AddMapping<Message>(typeof(Receiver));
            }
        }
    }
}