// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Linq;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    [TestFixture]
    public class Callbacks : SqlServerContext
    {
        EndpointDefinition sourceEndpointDefinition;
        EndpointDefinition competingEndpointDefinition;
        EndpointDefinition destinationEndpointDefinition;

        [SetUp]
        public void SetUp()
        {
            sourceEndpointDefinition = new EndpointDefinition("Source") {MachineName = Environment.MachineName + "_A"};
            competingEndpointDefinition = new EndpointDefinition("Source") {MachineName = Environment.MachineName + "_B"};
            destinationEndpointDefinition = new EndpointDefinition("Destination");
        }

        [Test]
        public void Roundtrip_1_2_to_2_2()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c => c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
            Action<IEndpointConfigurationV2> destinationConfig = c => { };

            VerifyRoundtrip("1.2", sourceConfig, sourceConfig, "2.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_1_2_to_3_0()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c => c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
            Action<IEndpointConfigurationV3> destinationConfig = c => { };

            VerifyRoundtrip("1.2", sourceConfig, sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        [Ignore("Callbacks does not work between 2.X and 1.X because 2.X uses a custom header")]
        public void Roundtrip_2_2_to_1_2()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
                c.EnableCallbacks();
            };
            Action<IEndpointConfigurationV1> destinationConfig = c => { };

            VerifyRoundtrip("2.2", sourceConfig, sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_2_2_to_3_0()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.EnableCallbacks();
            };
            Action<IEndpointConfigurationV3> destinationConfig = c => { };

            VerifyRoundtrip("2.2", sourceConfig, sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_1_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.EnableCallbacks("1");
                c.RouteToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
            };

            Action<IEndpointConfigurationV3> competingConfig = c =>
            {
                c.EnableCallbacks("2");
                c.RouteToEndpoint(typeof(TestRequest), $"Destination.{Environment.MachineName}");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c => { };

            VerifyRoundtrip("3.0", sourceConfig, competingConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Roundtrip_3_0_to_2_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.EnableCallbacks("1");
                c.RouteToEndpoint(typeof(TestRequest), "Destination");
            };

            Action<IEndpointConfigurationV3> competingConfig = c =>
            {
                c.EnableCallbacks("2");
                c.RouteToEndpoint(typeof(TestRequest), "Destination");
            };
            Action<IEndpointConfigurationV2> destinationConfig = c => { };

            VerifyRoundtrip("3.0", sourceConfig, competingConfig, "2.2", destinationConfig);
        }

        void VerifyRoundtrip<S, D>(string initiatorVersion, Action<S> initiatorConfig, Action<S> cometingConfig, string replierVersion, Action<D> replierConfig)
            where S : IEndpointConfiguration
            where D : IEndpointConfiguration
        {
            using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpointDefinition, initiatorVersion, initiatorConfig))
            {
                using (var competing = EndpointFacadeBuilder.CreateAndConfigure(competingEndpointDefinition, initiatorVersion, cometingConfig))
                {
                    using (EndpointFacadeBuilder.CreateAndConfigure(destinationEndpointDefinition, replierVersion, replierConfig))
                    {
                        var requestIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
                        foreach (var requestId in requestIds)
                        {
                            source.SendRequest(requestId);
                        }

                        //Wait till all five responses arrive at the initiator.

                        // ReSharper disable once AccessToDisposedClosure
                        AssertEx.WaitUntilIsTrue(() => requestIds.All(id => source.ReceivedResponseIds.Contains(id)));
                        Console.WriteLine("Done");
                    }
                }
            }
        }
    }
}