// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Linq;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    //TODO: MSDTC is not available on this Computer - we should have a better handling for message processing exceptions.
    [TestFixture]
    public class Callbacks
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
        public void Callback_1_2_to_2_2()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestIntCallback), "Destination");
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifyRoundtrip("1.2", sourceConfig, sourceConfig, "2.2", destinationConfig);
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

            VerifyRoundtrip("1.2", sourceConfig, sourceConfig, "3.0", destinationConfig);
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
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifyRoundtrip("2.2", sourceConfig, sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Callback_2_2_to_3_0()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestIntCallback), destinationEndpointDefinition.Name);
                c.EnableCallbacks();
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.EnableCallbacks(string.Empty, false);
            };

            VerifyRoundtrip("2.2", sourceConfig, sourceConfig, "3.0", destinationConfig);
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
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifyRoundtrip("3.0", sourceConfig, competingConfig, "1.2", destinationConfig);
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
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifyRoundtrip("3.0", sourceConfig, competingConfig, "2.2", destinationConfig);
        }

        void VerifyRoundtrip<S, D>(string initiatorVersion, Action<S> initiatorConfig, Action<S> cometingConfig, string replierVersion, Action<D> replierConfig)
            where S : IEndpointConfiguration
            where D : IEndpointConfiguration
        {
            using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpointDefinition, initiatorVersion, initiatorConfig))
            {
               using (EndpointFacadeBuilder.CreateAndConfigure(competingEndpointDefinition, initiatorVersion, cometingConfig))
               {
                    using (EndpointFacadeBuilder.CreateAndConfigure(destinationEndpointDefinition, replierVersion, replierConfig))
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
            }
        }
    }
}