// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Linq;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    [TestFixture]
    public partial class MessageExchangePatterns
    {
        [Test]
        public void Roundtrip_1_2_to_2_2()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifyRoundtrip("1.2", sourceConfig, "2.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_1_2_to_3_0()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifyRoundtrip("1.2", sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_1_2()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifyRoundtrip("2.2", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_3_0()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifyRoundtrip("2.2", sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_1_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.RouteToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifyRoundtrip("3.0", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_2_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.RouteToEndpoint(typeof(TestRequest), "Destination");
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifyRoundtrip("3.0", sourceConfig, "2.2", destinationConfig);
        }

        void VerifyRoundtrip<S, D>(string initiatorVersion, Action<S> initiatorConfig, string replierVersion, Action<D> replierConfig)
            where S : IEndpointConfiguration
            where D : IEndpointConfiguration
        {

            using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpointDefinition, initiatorVersion, initiatorConfig))
            {
                using (EndpointFacadeBuilder.CreateAndConfigure(destinationEndpointDefinition, replierVersion, replierConfig))
                {
                    var requestId = Guid.NewGuid();

                    source.SendRequest(requestId);

                    // ReSharper disable once AccessToDisposedClosure
                    AssertEx.WaitUntilIsTrue(() => source.ReceivedResponseIds.Any(responseId => responseId == requestId));
                }
            }
        }
    }
}