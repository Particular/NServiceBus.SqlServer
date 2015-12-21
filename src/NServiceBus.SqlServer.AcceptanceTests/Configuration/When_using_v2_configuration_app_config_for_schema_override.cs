namespace NServiceBus.SqlServer.AcceptanceTests.Configuration
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_v2_configuration_app_config_for_schema_override : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_fail_on_startup()
        {
            var appConfigFilename = $"app.sqlv2.config";
            var expectedErrorMessage = "Schema override in connection string is not supported anymore";

            File.WriteAllText(appConfigFilename,  
                        $@"<?xml version='1.0' encoding='utf-8'?>
                            <configuration>
                                <connectionStrings>
                                  <clear />
                                  <add name=""NServiceBus/Transport"" connectionString=""Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;Queue Schema=nsb""/>
                                </connectionStrings>
                            </configuration>");

            using (AppConfig.Change(appConfigFilename))
            {
                var scenario = await Scenario.Define<Context>()
                    .WithEndpoint<Endpoint>()
                    .Run();

                Assert.IsTrue(scenario.Exceptions.Count > 0);
                Assert.IsTrue(scenario.Exceptions.Any(e => e.Message.Contains(expectedErrorMessage)));
            }
        }

        public class Context : ScenarioContext
        {
        }


        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }
    }
}