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
        public void Send_1_2_to_2_2()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c => c.MapMessageToEndpoint(typeof(TestCommand), "Destination");
            Action<IEndpointConfigurationV2> destinationConfig = c => { };

            VerifySend("1.2", sourceConfig, "2.2", destinationConfig);
        }

        [Test]
        public void Send_1_2_to_3_0()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c => c.MapMessageToEndpoint(typeof(TestCommand), "Destination");
            Action<IEndpointConfigurationV3> destinationConfig = c => { };

            VerifySend("1.2", sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Send_2_2_to_1_2()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c => c.MapMessageToEndpoint(typeof(TestCommand), $"Destination.{Environment.MachineName}");
            Action<IEndpointConfigurationV1> destinationConfig = c => { };
            
            VerifySend("2.2", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Send_2_2_to_3_0()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c => c.MapMessageToEndpoint(typeof(TestCommand), "Destination");
            Action<IEndpointConfigurationV3> destinationConfig = c => { };
            
            VerifySend("2.2", sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Send_3_0_to_1_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c => c.RouteToEndpoint(typeof(TestCommand), $"Destination.{Environment.MachineName}");
            Action<IEndpointConfigurationV1> destinationConfig = c => { };
            
            VerifySend("3.0", sourceConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Send_3_0_to_2_2()
        {
            Action<IEndpointConfigurationV3> sourceConfig = c => c.RouteToEndpoint(typeof(TestCommand), "Destination");
            Action<IEndpointConfigurationV2> destinationConfig = c => { };
            
            VerifySend("3.0", sourceConfig, "2.2", destinationConfig);
        }
        
        void VerifySend<S, D>(string sourceVersion, Action<S> sourceConfig, string destinationVersion, Action<D> destinationConfig)
            where S : IEndpointConfiguration
            where D : IEndpointConfiguration
        {
            using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpointDefinition, sourceVersion, sourceConfig))
            {
                using (var destination = EndpointFacadeBuilder.CreateAndConfigure(destinationEndpointDefinition, destinationVersion, destinationConfig))
                {
                    var messageId = Guid.NewGuid();

                    source.SendCommand(messageId);

                    // ReSharper disable once AccessToDisposedClosure
                    AssertEx.WaitUntilIsTrue(() => destination.ReceivedMessageIds.Any(mi => mi == messageId));
                }
            }
        }
    }
}