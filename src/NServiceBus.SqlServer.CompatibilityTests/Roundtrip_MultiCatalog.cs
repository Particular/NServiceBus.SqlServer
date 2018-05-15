// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Collections.Generic;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;
    using Support;

    [TestFixture]
    public partial class Roundtrip
    {
        [Test]
        public void Roundtrip_1_2_to_3_1_in_different_catalogs()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
                c.ConfigureNamedConnectionStringForAddress(destinationEndpoint.Name, ConnectionStrings.Instance2);
            };
            Action<IEndpointConfigurationV3_1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.UseCatalogForQueue($"{sourceEndpoint.Name}.{Environment.MachineName}", "nservicebus1");
            };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_3_1_in_different_catalogs()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
                c.UseConnectionStringForAddress(destinationEndpoint.Name, ConnectionStrings.Instance2);
            };
            Action<IEndpointConfigurationV3_1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.UseCatalogForQueue(sourceEndpoint.Name, "nservicebus1");
            };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_3_1_in_different_catalogs()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.RouteToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    [destinationEndpoint.Name] = ConnectionStrings.Instance2,
                    [""] = ConnectionStrings.Instance1, //All other addresses match here
                });
            };
            Action<IEndpointConfigurationV3_1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.UseCatalogForQueue(sourceEndpoint.Name, "nservicebus1");
            };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_3_1_to_3_0_in_different_catalogs()
        {
            Action<IEndpointConfigurationV3_1> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.RouteToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
                c.UseCatalogForQueue(destinationEndpoint.Name, "nservicebus2");
            };
            Action<IEndpointConfigurationV3_1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    [sourceEndpoint.Name] = ConnectionStrings.Instance1,
                    [""] = ConnectionStrings.Instance2, //All other addresses match here
                });
            };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_3_1_to_2_2_in_different_catalogs()
        {
            Action<IEndpointConfigurationV3_1> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.RouteToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
                c.UseCatalogForQueue(destinationEndpoint.Name, "nservicebus2");
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.UseConnectionStringForAddress(sourceEndpoint.Name, ConnectionStrings.Instance1);
            };
            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_3_1_to_1_2_in_different_catalogs()
        {
            Action<IEndpointConfigurationV3_1> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.RouteToEndpoint(typeof(TestRequest), $"{destinationEndpoint.Name}.{RuntimeEnvironment.MachineName}");
                c.UseCatalogForQueue($"{destinationEndpoint.Name}.{RuntimeEnvironment.MachineName}", "nservicebus2");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.ConfigureNamedConnectionStringForAddress(sourceEndpoint.Name, ConnectionStrings.Instance1);
            };
            VerifyRoundtrip(sourceConfig, destinationConfig);
        }
    }
}