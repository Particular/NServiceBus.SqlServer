
namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using System;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_custom_catalog_configured_for_local_endpoint : MultiCatalogAcceptanceTest
    {

        [Test]
        public async Task Should_throw()
        {
            var endpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Receiver));

            await Scenario.Define<ScenarioContext>()
                .WithEndpoint<Receiver>(b => b.CustomConfig(c =>
                {
                    Assert.Throws<ArgumentException>(
                        () => c.ConfigureRouting().UseCatalogForEndpoint(endpointName, "some-catalog"),
                        "Custom catalog configuration is not supported for a local endpoint");
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