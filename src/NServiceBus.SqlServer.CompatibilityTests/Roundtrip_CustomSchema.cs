// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    //TODO: add catalog and schema creation scripts to auto-run on setup
    [TestFixture]
    public partial class Roundtrip
    {
        [Test]
        public void Roundtrip_1_2_to_2_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
                c.UseConnectionString(ConnectionStrings.Instance1_Src);
                c.ConfigureNamedConnectionStringForAddress(destinationEndpoint.Name, ConnectionStrings.Instance1_Dest);
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.DefaultSchema(ConnectionStrings.Schema_Dest);
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.UseSchemaForTransportAddress($"{sourceEndpoint.Name}.{Environment.MachineName}", ConnectionStrings.Schema_Src);
            };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_1_2_to_3_0_with_custom_schemas()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
                c.UseConnectionString(ConnectionStrings.Instance1_Src);
                c.ConfigureNamedConnectionStringForAddress(destinationEndpoint.Name, ConnectionStrings.Instance1_Dest);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema(ConnectionStrings.Schema_Dest);
                c.UseSchemaForQueue($"{sourceEndpoint.Name}.{Environment.MachineName}", ConnectionStrings.Schema_Src);
            };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_1_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestRequest), $"{destinationEndpoint.Name}.{Environment.MachineName}");
                c.DefaultSchema(ConnectionStrings.Schema_Src);
                c.UseSchemaForTransportAddress($"{destinationEndpoint.Name}.{Environment.MachineName}", ConnectionStrings.Schema_Dest);
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1_Dest);
                c.ConfigureNamedConnectionStringForAddress(sourceEndpoint.Name, ConnectionStrings.Instance1_Src);
            };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_3_0_with_custom_schemas()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
                c.DefaultSchema(ConnectionStrings.Schema_Src);
                c.UseSchemaForTransportAddress(destinationEndpoint.Name, ConnectionStrings.Schema_Dest);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema(ConnectionStrings.Schema_Dest);
                c.UseSchemaForQueue(sourceEndpoint.Name, ConnectionStrings.Schema_Src);
            };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_1_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema(ConnectionStrings.Schema_Src);
                c.RouteToEndpoint(typeof(TestRequest), $"{destinationEndpoint.Name}.{Environment.MachineName}");
                c.UseSchemaForEndpoint($"{destinationEndpoint.Name}.{Environment.MachineName}", ConnectionStrings.Schema_Dest);
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1_Dest);
                c.ConfigureNamedConnectionStringForAddress(sourceEndpoint.Name, ConnectionStrings.Instance1_Src);
            };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_2_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema(ConnectionStrings.Schema_Src);
                c.RouteToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
                c.UseSchemaForEndpoint(destinationEndpoint.Name, ConnectionStrings.Schema_Dest);
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema(ConnectionStrings.Schema_Dest);
                c.UseSchemaForTransportAddress(sourceEndpoint.Name, ConnectionStrings.Schema_Src);
            };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }
    }
}