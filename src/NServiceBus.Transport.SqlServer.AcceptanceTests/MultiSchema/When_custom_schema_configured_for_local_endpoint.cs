namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiSchema
{
    using System;
    using NUnit.Framework;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    public class When_custom_schema_configured_for_local_endpoint : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_throw()
        {
            var endpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Receiver));

            await Scenario.Define<ScenarioContext>()
                .WithEndpoint<Receiver>(b => b.CustomConfig(
                    c =>
                    {
                        Assert.Throws<ArgumentException>(
                            () => c.ConfigureRouting().UseSchemaForEndpoint(endpointName, "some-schema"),
                            "Custom schema configuration is not supported for local endpoint.");
                    }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.Pass();
        }


        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }
        }
    }
}