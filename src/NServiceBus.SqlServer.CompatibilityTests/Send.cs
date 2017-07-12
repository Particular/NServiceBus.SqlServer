// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Linq;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    [TestFixture]
    public class Send
    {
        [Test]
        public void Send_1_2_to_2_2()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestCommand), destinationEndpoint.Name);
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifySend(sourceConfig, destinationConfig);
        }

        [Test]
        public void Send_1_2_to_3_0()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestCommand), destinationEndpoint.Name);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifySend(sourceConfig, destinationConfig);
        }

        [Test]
        public void Send_2_2_to_1_2()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestCommand), $"{destinationEndpoint.Name}.{Environment.MachineName}");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifySend(sourceConfig, destinationConfig);
        }

        [Test]
        public void Send_2_2_to_3_0()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestCommand), destinationEndpoint.Name);
            };
            Action<IEndpointConfigurationV3> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifySend(sourceConfig, destinationConfig);
        }

        [Test]
        public void Send_3_0_to_1_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.RouteToEndpoint(typeof(TestCommand), $"{destinationEndpoint.Name}.{Environment.MachineName}");
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifySend(sourceConfig, destinationConfig);
        }

        [Test]
        public void Send_3_0_to_2_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.RouteToEndpoint(typeof(TestCommand), destinationEndpoint.Name);
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };

            VerifySend(sourceConfig, destinationConfig);
        }

        [SetUp]
        public void SetUp()
        {
            sourceEndpoint = new EndpointDefinition("Source");
            destinationEndpoint = new EndpointDefinition("Destination");
        }

        void VerifySend<S, D>(Action<S> sourceConfig, Action<D> destinationConfig)
            where S : IEndpointConfiguration
            where D : IEndpointConfiguration
        {
            using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpoint, sourceConfig))
            using (var destination = EndpointFacadeBuilder.CreateAndConfigure(destinationEndpoint, destinationConfig))
            {
                var messageId = Guid.NewGuid();

                source.SendCommand(messageId);

                // ReSharper disable once AccessToDisposedClosure
                AssertEx.WaitUntilIsTrue(() => destination.ReceivedMessageIds.Any(mi => mi == messageId));
            }
        }

        EndpointDefinition sourceEndpoint;
        EndpointDefinition destinationEndpoint;
    }
}