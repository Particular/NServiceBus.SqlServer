// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    //TODO: add catalog and schema creation scripts to auto-run on setup
    [TestFixture]
    public partial class MessageExchangePatterns
    {
        [Test]
        public void Roundtrip_1_2_to_2_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.UseConnectionString(ConnectionStrings.Instance1_Src);
                c.ConfigureNamedConnectionStringForAddress("Destination", ConnectionStrings.Instance1_Dest);
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.DefaultSchema("dest");
                c.UseConnectionString(ConnectionStrings.Instance1);
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
                c.UseConnectionString(ConnectionStrings.Instance1_Src);
                c.ConfigureNamedConnectionStringForAddress("Destination", ConnectionStrings.Instance1_Dest);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
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
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
                c.DefaultSchema("src");
                c.UseSchemaForTransportAddress($"Destination.{Environment.MachineName}", "dest");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1_Dest);
                c.ConfigureNamedConnectionStringForAddress("Source", ConnectionStrings.Instance1_Src);
            };

            VerifyRoundtrip("2.2", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_3_0_with_custom_schemas()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.DefaultSchema("src");
                c.UseSchemaForTransportAddress("Destination", "dest");
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
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
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("src");
                c.RouteToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
                c.UseSchemaForEndpoint($"Destination.{Environment.MachineName}", "dest");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1_Dest);
                c.ConfigureNamedConnectionStringForAddress("Source", ConnectionStrings.Instance1_Src);
            };

            VerifyRoundtrip("3.0", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_2_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("src");
                c.RouteToEndpoint(typeof(TestRequest), "Destination");
                c.UseSchemaForEndpoint("Destination", "dest");
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("dest");
                c.UseSchemaForTransportAddress("Source", "src");
            };

            VerifyRoundtrip("3.0", sourceConfig, "2.2", destinationConfig);
        }
    }
}