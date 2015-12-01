namespace NServiceBus.SqlServer.AcceptanceTests
{
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    public class When_custom_schema_configured_with_default_override : When_custom_schema_configured
    {
        [Test]
        public async void Should_recieive_message()
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
                    c.UseTransport<SqlServerTransport>().DefaultSchema(ReceiverSchema);
                }).AddMapping<Message>(typeof(Receiver));
            }
        }
    }
}
 