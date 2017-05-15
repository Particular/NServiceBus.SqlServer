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
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.ConfigureNamedConnectionStringForAddress("Destination", ConnectionStrings.Instance2);
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.UseConnectionStringForAddress($"Source.{Environment.MachineName}", ConnectionStrings.Instance1);
            };

            VerifyRoundtrip("1.2", sourceConfig, "2.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_1_2_to_3_0_on_different_instances()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.ConfigureNamedConnectionStringForAddress("Destination", ConnectionStrings.Instance2);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    ["Source"] = ConnectionStrings.Instance1,
                    [""]       = ConnectionStrings.Instance2 //All other addresses match here
                });
            };

            VerifyRoundtrip("1.2", sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_1_2_on_different_instances()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
                c.UseConnectionStringForAddress($"Destination.{Environment.MachineName}", ConnectionStrings.Instance2);
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.ConfigureNamedConnectionStringForAddress("Source", ConnectionStrings.Instance1);
            };

            VerifyRoundtrip("2.2", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_3_0_on_different_instances()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.UseConnectionStringForAddress("Destination", ConnectionStrings.Instance2);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    ["Source"] = ConnectionStrings.Instance1,
                    [""] = ConnectionStrings.Instance2, //All other addresses match here
                });
            };

            VerifyRoundtrip("2.2", sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_1_2_on_different_instances()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.RouteToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    [$"Destination.{Environment.MachineName}"] = ConnectionStrings.Instance2,
                    [""] = ConnectionStrings.Instance1, //All other addresses match here
                });
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.ConfigureNamedConnectionStringForAddress("Source", ConnectionStrings.Instance1);
            };

            VerifyRoundtrip("3.0", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_2_2_on_different_instances()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.RouteToEndpoint(typeof(TestRequest), "Destination");
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    ["Destination"] = ConnectionStrings.Instance2,
                    [""] = ConnectionStrings.Instance1, //All other addresses match here
                });
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.UseConnectionStringForAddress("Source", ConnectionStrings.Instance1);
            };

            VerifyRoundtrip("3.0", sourceConfig, "2.2", destinationConfig);
        }

        //TODO: Do we need ver x <-> ver x test? Should be covered by integration tests
        [Test]
        public void Roundtrip_3_0_to_3_0_on_different_instances()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("src");
                c.RouteToEndpoint(typeof(TestRequest), "Destination");
                c.UseSchemaForEndpoint("Destination", "dest");
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("dest");
            };

            VerifyRoundtrip("3.0", sourceConfig, "3.0", destinationConfig);
        }
    }
}