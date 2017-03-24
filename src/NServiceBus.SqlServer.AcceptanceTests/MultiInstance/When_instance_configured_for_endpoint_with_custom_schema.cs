namespace NServiceBus.SqlServer.AcceptanceTests.MultiInstance
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NUnit.Framework;
    using Transport.SQLServer;

    public class When_instance_configured_for_endpoint_with_custom_schema : When_instance_configured
    {
        [Test]
        public Task Should_receive_message()
        {
            return Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new Message())))
                .WithEndpoint<Spy>()
                .Done(c => c.MessageReceived)
                .Repeat(r => r.For(Transports.Default))
                .Should(c =>
                {
                    Assert.True(c.MessageReceived, "Message should be properly received");
                    var forwardHeader = c.Headers["NServiceBus.SqlServer.ForwardTo"];
                    Assert.AreEqual("UltimateReceiver@[schema]@[catalog]@[ReceiverInstance]", forwardHeader);
                })
                .Run();
        }
        
        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var settings = c.UseTransport<SqlServerTransport>();
                    settings.Routing().RouteToEndpoint(typeof(Message), "UltimateReceiver");
                    settings.UseInstanceForEndpoint("UltimateReceiver", "ReceiverInstance");
                    settings.UseSchemaForEndpoint("UltimateReceiver", "schema");
                    settings.UseCatalogForEndpoint("UltimateReceiver", "catalog");
                    settings.ForwardMessagesToOtherInstancesVia(SpyAddress);
                });
            }
        }
    }
}