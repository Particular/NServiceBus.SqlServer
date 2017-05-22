// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Linq;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    [TestFixture]
    public class Callback
    {
        [Test]
        public void Callback_1_2_to_2_2()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestIntCallback), "Destination");
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV2> destinationConfig = c => { c.UseConnectionString(ConnectionStrings.Default); };

            VerifyRoundtrip(sourceConfig, sourceConfig, destinationConfig);
        }

        [Test]
        public void Callback_1_2_to_3_0()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestIntCallback), "Destination");
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.EnableCallbacks(string.Empty, false);
            };

            VerifyRoundtrip(sourceConfig, sourceConfig, destinationConfig);
        }

        [Test]
        [Ignore("Callbacks does not work between 2.X and 1.X because 2.X uses a custom header")]
        public void Callback_2_2_to_1_2()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestIntCallback), $"Destination.{Environment.MachineName}");
                c.EnableCallbacks();
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV1> destinationConfig = c => { c.UseConnectionString(ConnectionStrings.Default); };

            VerifyRoundtrip(sourceConfig, sourceConfig, destinationConfig);
        }

        [Test]
        public void Callback_2_2_to_3_0()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestIntCallback), destinationEndpoint.Name);
                c.EnableCallbacks();
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.EnableCallbacks(string.Empty, false);
            };

            VerifyRoundtrip(sourceConfig, sourceConfig, destinationConfig);
        }

        [Test]
        public void Callback_3_0_to_1_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.EnableCallbacks("1");
                c.RouteToEndpoint(typeof(TestIntCallback), $"Destination.{Environment.MachineName}");
                c.UseConnectionString(ConnectionStrings.Default);
            };

            Action<IEndpointConfigurationV3> competingConfig = c =>
            {
                c.EnableCallbacks("2");
                c.RouteToEndpoint(typeof(TestIntCallback), $"Destination.{Environment.MachineName}");
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV1> destinationConfig = c => { c.UseConnectionString(ConnectionStrings.Default); };

            VerifyRoundtrip(sourceConfig, competingConfig, destinationConfig);
        }

        //TODO: fix all the callback test to actually use callbacks. Update to the newest Callbacks package in v6
        [Test]
        public void Callback_3_0_to_2_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.EnableCallbacks("1");
                c.RouteToEndpoint(typeof(TestIntCallback), "Destination");
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV3> competingConfig = c =>
            {
                c.EnableCallbacks("2");
                //HINT: this is not really needed
                c.RouteToEndpoint(typeof(TestIntCallback), "Destination");
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV2> destinationConfig = c => { c.UseConnectionString(ConnectionStrings.Default); };

            VerifyRoundtrip(sourceConfig, competingConfig, destinationConfig);
        }

        [SetUp]
        public void SetUp()
        {
            sourceEndpoint = new EndpointDefinition("Source")
            {
                MachineName = Environment.MachineName + "_A"
            };
            competingEndpoint = new EndpointDefinition("Source")
            {
                MachineName = Environment.MachineName + "_B"
            };
            destinationEndpoint = new EndpointDefinition("Destination");
        }

        void VerifyRoundtrip<S, D>(Action<S> sourceConfig, Action<S> competingSourceConfig, Action<D> destinationConfig)
            where S : IEndpointConfiguration
            where D : IEndpointConfiguration
        {
            using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpoint, sourceConfig))
            using (EndpointFacadeBuilder.CreateAndConfigure(competingEndpoint, competingSourceConfig))
            using (EndpointFacadeBuilder.CreateAndConfigure(destinationEndpoint, destinationConfig))
            {
                var firstValue = new Random().Next(1000);
                var values = Enumerable.Range(0, 5).Select(i => firstValue + i).ToArray();

                foreach (var value in values)
                {
                    source.SendAndCallbackForInt(value);
                }

                //Wait till all five responses arrive at the initiator.

                // ReSharper disable once AccessToDisposedClosure
                AssertEx.WaitUntilIsTrue(() => values.All(value => source.ReceivedIntCallbacks.Contains(value)));
                Console.WriteLine("Done");
            }
        }

        EndpointDefinition sourceEndpoint;
        EndpointDefinition competingEndpoint;
        EndpointDefinition destinationEndpoint;
    }
}