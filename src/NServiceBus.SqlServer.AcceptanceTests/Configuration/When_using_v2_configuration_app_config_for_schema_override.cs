namespace NServiceBus.SqlServer.AcceptanceTests.Configuration
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_v2_configuration_app_config_for_schema_override : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_fail_on_startup()
        {
            var appConfigFilename = "app.sqlv2.config";
            var expectedErrorMessage = "Schema override in connection string is not supported anymore";

            File.WriteAllText(appConfigFilename,
                @"<?xml version='1.0' encoding='utf-8'?>
                            <configuration>
                                <connectionStrings>
                                  <clear />
                                  <add name=""NServiceBus/Transport"" connectionString=""Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;Queue Schema=nsb""/>
                                </connectionStrings>
                            </configuration>");

            using (AppConfig.Change(appConfigFilename))
            {
                try
                {
                    await Scenario.Define<Context>()
                        .WithEndpoint<Endpoint>()
                        .Run();
                }
                catch (AggregateException exception)
                {
                    Assert.IsTrue(exception.InnerExceptions.Count > 0);
                    Assert.IsTrue(exception.InnerExceptions.Any(e => e.InnerException != null && e.InnerException.Message.Contains(expectedErrorMessage)));

                    return;
                }

                Assert.Fail("Endpoint should fail on startup due to unsupported v2 configuration.");
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