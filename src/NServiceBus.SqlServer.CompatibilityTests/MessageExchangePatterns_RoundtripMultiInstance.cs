// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Collections.Generic;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    [TestFixture]
    public partial class MessageExchangePatterns
    {
        [Test]
        public void Roundtrip_1_2_to_2_2_on_different_instances()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.ConfigureNamedConnectionStringForAddress("Destination", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;");
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;");
                c.UseConnectionStringForAddress($"Source.{Environment.MachineName}", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;");
            };

            VerifyRoundtrip("1.2", sourceConfig, "2.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_1_2_to_3_0_on_different_instances()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.ConfigureNamedConnectionStringForAddress("Destination", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;");
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;");
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    ["Source"] = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;",
                    [""] = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;", //All other addresses match here
                });
            };

            VerifyRoundtrip("1.2", sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_1_2_on_different_instances()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
                c.UseConnectionStringForAddress($"Destination.{Environment.MachineName}", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True");
                c.ConfigureNamedConnectionStringForAddress("Source", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;");
            };

            VerifyRoundtrip("2.2", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_3_0_on_different_instances()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.UseConnectionStringForAddress("Destination", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;");
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;");
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    ["Source"] = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;",
                    [""] = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;", //All other addresses match here
                });
            };

            VerifyRoundtrip("2.2", sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_1_2_on_different_instances()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.RouteToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    [$"Destination.{Environment.MachineName}"] = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;",
                    [""] = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;", //All other addresses match here
                });
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True");
                c.ConfigureNamedConnectionStringForAddress("Source", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;");
            };

            VerifyRoundtrip("3.0", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_2_2_on_different_instances()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.RouteToEndpoint(typeof(TestRequest), "Destination");
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    ["Destination"] = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;",
                    [""] = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;", //All other addresses match here
                });
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;");
                c.UseConnectionStringForAddress("Source", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;");
            };

            VerifyRoundtrip("3.0", sourceConfig, "2.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_3_0_on_different_instances()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.DefaultSchema("src");
                c.RouteToEndpoint(typeof(TestRequest), "Destination");
                c.UseSchemaForEndpoint("Destination", "dest");
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.DefaultSchema("dest");
            };

            VerifyRoundtrip("3.0", sourceConfig, "3.0", destinationConfig);
        }
    }
}