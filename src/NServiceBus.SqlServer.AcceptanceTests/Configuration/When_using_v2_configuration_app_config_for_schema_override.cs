namespace NServiceBus.SqlServer.AcceptanceTests.Configuration
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_v2_configuration_app_config_for_schema_override : NServiceBusAcceptanceTest
    {
        [Test]
        public Task Should_fail_on_startup()
        {
            var appConfigFilename = "app.sqlv2.config";
            var appConfigPath = Path.Combine(Directory.GetCurrentDirectory(), appConfigFilename);

            var expectedErrorMessage = "Schema override in connection string is not supported anymore";

            File.WriteAllText(appConfigPath,
                @"<?xml version='1.0' encoding='utf-8'?>
                            <configuration>
                                <connectionStrings>
                                  <clear />
                                  <add name=""NServiceBus/Transport"" connectionString=""Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;Queue Schema=nsb""/>
                                </connectionStrings>
                            </configuration>");

            using (AppConfig.Change(appConfigPath))
            {
                var exception = Assert.ThrowsAsync(Is.AssignableTo<Exception>(), async () =>
                {
                    await Scenario.Define<Context>()
                        .WithEndpoint<Endpoint>()
                        .Run();
                }, "Endpoint startup should throw and exception");

                Assert.That(exception.Message, Contains.Substring(expectedErrorMessage),  "Endpoint should fail on startup due to unsupported v2 configuration.");

                return Task.FromResult(0);
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