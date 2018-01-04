namespace NServiceBus.SqlServer.AcceptanceTests.Configuration
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NUnit.Framework;

    public class When_using_v2_configuration_app_config_for_schema_override : NServiceBusAcceptanceTest
    {
        [Test]
        public Task Should_fail_on_startup()
        {
            var appConfigPath = CreateV2ConfigurationFile();

            using (AppConfig.Change(appConfigPath))
            {
                var exception = Assert.ThrowsAsync(Is.AssignableTo<Exception>(), async () =>
                {
                    await Scenario.Define<Context>()
                        .WithEndpoint<Endpoint>()
                        .Run();
                }, "Endpoint startup should throw and exception");

                var expectedErrorMessage = "Schema override in connection string is not supported anymore";

                Assert.That(exception.Message, Contains.Substring(expectedErrorMessage), "Endpoint should fail on startup due to unsupported v2 configuration.");
            }

            return Task.FromResult(0);
        }

        [Test]
        public Task Should_work_when_connection_string_validation_is_disabled()
        {
            var appConfigPath = CreateV2ConfigurationFile();

            using (AppConfig.Change(appConfigPath))
            {
                Assert.DoesNotThrowAsync(async () =>
                {
                    await Scenario.Define<Context>()
                        .WithEndpoint<Endpoint>(c => c.CustomConfig(ec =>
                        {
                            ec.GetSettings().Set("SqlServer.DisableConnectionStringValidation", true);
                        }))
                    .Run();
                }, "Endpoint startup should not throw with connection string validation turned off.");
            }

            return Task.FromResult(0);
        }

        static string CreateV2ConfigurationFile()
        {
            var appConfigFilename = "app.sqlv2.config";
            var appConfigPath = Path.Combine(Directory.GetCurrentDirectory(), appConfigFilename);


            File.WriteAllText(appConfigPath,
                @"<?xml version='1.0' encoding='utf-8'?>
                            <configuration>
                                <connectionStrings>
                                  <clear />
                                  <add name=""NServiceBus/Transport"" connectionString=""Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;Queue Schema=nsb""/>
                                </connectionStrings>
                            </configuration>");
            return appConfigPath;
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