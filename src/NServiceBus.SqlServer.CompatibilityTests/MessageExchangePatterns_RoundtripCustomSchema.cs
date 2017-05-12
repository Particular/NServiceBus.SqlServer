// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    [TestFixture]
    public partial class MessageExchangePatterns
    {
        [Test]
        public void Roundtrip_1_2_to_2_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.UseConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;Queue Schema=src");
                c.ConfigureNamedConnectionStringForAddress("Destination", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;Queue Schema=dest");
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.DefaultSchema("dest");
                c.UseSchemaForTransportAddress($"Source.{Environment.MachineName}", "src");
            };

            VerifyRoundtrip("1.2", sourceConfig, "2.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_1_2_to_3_0_with_custom_schemas()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.UseConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;Queue Schema=src");
                c.ConfigureNamedConnectionStringForAddress("Destination", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;Queue Schema=dest");
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.DefaultSchema("dest");
                c.UseSchemaForQueue($"Source.{Environment.MachineName}", "src");
            };

            VerifyRoundtrip("1.2", sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_1_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
                c.DefaultSchema("src");
                c.UseSchemaForTransportAddress($"Destination.{Environment.MachineName}", "dest");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;Queue Schema=dest");
                c.ConfigureNamedConnectionStringForAddress("Source", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;Queue Schema=src");
            };

            VerifyRoundtrip("2.2", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_3_0_with_custom_schemas()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.DefaultSchema("src");
                c.UseSchemaForTransportAddress("Destination", "dest");
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.DefaultSchema("dest");
                c.UseSchemaForQueue($"Source.{Environment.MachineName}", "src");
            };

            VerifyRoundtrip("2.2", sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_1_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.DefaultSchema("src");
                c.RouteToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
                c.UseSchemaForEndpoint($"Destination.{Environment.MachineName}", "dest");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;Queue Schema=dest");
                c.ConfigureNamedConnectionStringForAddress("Source", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;Queue Schema=src");
            };

            VerifyRoundtrip("3.0", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_2_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.DefaultSchema("src");
                c.RouteToEndpoint(typeof(TestRequest), "Destination");
                c.UseSchemaForEndpoint("Destination", "dest");
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.DefaultSchema("dest");
                c.UseSchemaForTransportAddress("Source", "src");
            };

            VerifyRoundtrip("3.0", sourceConfig, "2.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_3_0_with_custom_schemas()
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