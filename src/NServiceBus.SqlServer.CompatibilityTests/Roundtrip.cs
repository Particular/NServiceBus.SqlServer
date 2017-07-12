// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Linq;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    [TestFixture]
    public partial class Roundtrip
    {
        [Test]
        public void Roundtrip_1_2_to_2_2()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
            };
            Action<IEndpointConfigurationV2> destinationConfig = c => { c.UseConnectionString(ConnectionStrings.Default); };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_1_2_to_3_0()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c => { c.UseConnectionString(ConnectionStrings.Default); };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_1_2()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestRequest), $"{destinationEndpoint.Name}.{Environment.MachineName}");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c => { c.UseConnectionString(ConnectionStrings.Default); };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_3_0()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c => { c.UseConnectionString(ConnectionStrings.Default); };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_1_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.RouteToEndpoint(typeof(TestRequest), $"{destinationEndpoint.Name}.{Environment.MachineName}");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c => { c.UseConnectionString(ConnectionStrings.Default); };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_2_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.RouteToEndpoint(typeof(TestRequest), destinationEndpoint.Name);
            };
            Action<IEndpointConfigurationV2> destinationConfig = c => { c.UseConnectionString(ConnectionStrings.Default); };

            VerifyRoundtrip(sourceConfig, destinationConfig);
        }

        [SetUp]
        public void SetUp()
        {
            sourceEndpoint = new EndpointDefinition("Source");
            destinationEndpoint = new EndpointDefinition("Destination");
        }

        void VerifyRoundtrip<S, D>(Action<S> initiatorConfig, Action<D> replierConfig)
            where S : IEndpointConfiguration
            where D : IEndpointConfiguration
        {
            using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpoint, initiatorConfig))
            using (EndpointFacadeBuilder.CreateAndConfigure(destinationEndpoint, replierConfig))
            {
                var requestId = Guid.NewGuid();

                source.SendRequest(requestId);

                // ReSharper disable once AccessToDisposedClosure
                AssertEx.WaitUntilIsTrue(() => source.ReceivedResponseIds.Any(responseId => responseId == requestId));
            }
        }

        EndpointDefinition sourceEndpoint;
        EndpointDefinition destinationEndpoint;
    }
}