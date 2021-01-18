﻿namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiSchema
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    using static AcceptanceTesting.Customization.Conventions;

    public class When_custom_schema_configured_for_endpoint_with_brackets_syntax : When_custom_schema_configured_for_endpoint
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
                    var receiverEndpoint = $"{EndpointNamingConvention(typeof(Receiver))}";

                    var transport = new SqlServerTransport();
                    transport.EndpointSchemaAndCatalogSettings.SpecifySchema(receiverEndpoint, $"[{ReceiverSchema}]");
                    
                    c.UseTransport(transport);
                    c.ConfigureRouting().RouteToEndpoint(typeof(Message), receiverEndpoint);
                });
            }
        }
    }
}