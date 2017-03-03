// ReSharper disable InconsistentNaming

namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Linq;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    [TestFixture]
    public class MessageExchangePatterns : SqlServerContext
    {
        SqlServerEndpointDefinition sourceEndpointDefinition;
        SqlServerEndpointDefinition destinationEndpointDefinition;

        [SetUp]
        public void SetUp()
        {
            sourceEndpointDefinition = new SqlServerEndpointDefinition
            {
                Name = "Source"
            };
            destinationEndpointDefinition = new SqlServerEndpointDefinition
            {
                Name = "Destination"
            };
        }

        [Test]
        public void Send_1_2_to_2_2()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c => c.MapMessageToEndpoint(typeof(TestCommand), "Destination");
            Action<IEndpointConfigurationV2> destinationConfig = c => { };

            VerifySend("1.2", sourceConfig, "2.2", destinationConfig);
        }

        [Test]
        public void Send_2_2_to_3_0()
        {
            Action<IEndpointConfigurationV2> sourceConfig = c => c.MapMessageToEndpoint(typeof(TestCommand), "Destination");
            Action<IEndpointConfigurationV3> destinationConfig = c => { };
            
            VerifySend("2.2", sourceConfig, "3.0", destinationConfig);
        }

        [Test]
        public void Reply_1_2_to_2_2()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c => c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
            Action<IEndpointConfigurationV2> destinationConfig = c => { };

            VerifyReply("1.2", sourceConfig, "2.2", destinationConfig);
        }

        [Test]
        public void Reply_1_2_to_2_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV1> sourceConfig = c =>
            {
                c.MapMessageToEndpoint(typeof(TestRequest), "Destination");
                c.UseConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus-compat;Integrated Security=True;Queue Schema=src");
                c.UseConnectionStringForEndpoint("Destination", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus-compat;Integrated Security=True;Queue Schema=dest");
            };
            Action<IEndpointConfigurationV2> destinationConfig = c =>
            {
                c.DefaultSchema("dest");
                c.UseSchemaForTransportAddress("Source.SIMON-MAC", "src");
            };

            VerifyReply("1.2", sourceConfig, "2.2", destinationConfig);
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

        void VerifyReply<S, D>(string initiatorVersion, Action<S> initiatorConfig, string replierVersion, Action<D> replierConfig)
            where S : IEndpointConfiguration
            where D : IEndpointConfiguration
        {
            using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpointDefinition, initiatorVersion, initiatorConfig))
            {
                using (var destination = EndpointFacadeBuilder.CreateAndConfigure(destinationEndpointDefinition, replierVersion, replierConfig))
                {
                    var requestId = Guid.NewGuid();

                    source.SendRequest(requestId);

                    // ReSharper disable once AccessToDisposedClosure
                    AssertEx.WaitUntilIsTrue(() => source.ReceivedResponseIds.Any(responseId => responseId == requestId));
                }
            }
        }


        //[Test, TestCaseSource(nameof(GenerateVersionsPairs))]
        //public void It_is_possible_to_send_command_between_different_versions(string sourceVersion, string destinationVersion)
        //{
        //    sourceEndpointDefinition.Mappings = new[]
        //    {
        //        new MessageMapping
        //        {
        //            MessageType = typeof(TestCommand),
        //            TransportAddress = destinationEndpointDefinition.TransportAddressForVersion(destinationVersion)
        //        }
        //    };

        //    using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpointDefinition, sourceVersion))
        //    {
        //        using (var destination = EndpointFacadeBuilder.CreateAndConfigure(destinationEndpointDefinition, destinationVersion))
        //        {
        //            var messageId = Guid.NewGuid();

        //            source.SendCommand(messageId);

        //            // ReSharper disable once AccessToDisposedClosure
        //            AssertEx.WaitUntilIsTrue(() => destination.ReceivedMessageIds.Any(mi => mi == messageId));
        //        }
        //    }
        //}

        //[Test, TestCaseSource(nameof(GenerateVersionsPairs))]
        //public void It_is_possible_to_send_request_and_receive_replay(string sourceVersion, string destinationVersion)
        //{
        //    sourceEndpointDefinition.Mappings = new[]
        //    {
        //        new MessageMapping
        //        {
        //            MessageType = typeof(TestRequest),
        //            TransportAddress = destinationEndpointDefinition.TransportAddressForVersion(destinationVersion)
        //        }
        //    };

        //    using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpointDefinition, sourceVersion))
        //    {
        //        using (EndpointFacadeBuilder.CreateAndConfigure(destinationEndpointDefinition, destinationVersion))
        //        {
        //            var requestId = Guid.NewGuid();

        //            source.SendRequest(requestId);

        //            // ReSharper disable once AccessToDisposedClosure
        //            AssertEx.WaitUntilIsTrue(() => source.ReceivedResponseIds.Any(responseId => responseId == requestId));
        //        }
        //    }
        //}

        //[Test, TestCaseSource(nameof(GenerateVersionsPairs))]
        //public void It_is_possible_to_publish_events(string sourceVersion, string destinationVersion)
        //{
        //    destinationEndpointDefinition.Mappings = new[]
        //    {
        //        new MessageMapping
        //        {
        //            MessageType = typeof(TestEvent),
        //            TransportAddress = sourceEndpointDefinition.TransportAddressForVersion(sourceVersion)
        //        }
        //    };

        //    using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpointDefinition, sourceVersion))
        //    {
        //        using (var destination = EndpointFacadeBuilder.CreateAndConfigure(destinationEndpointDefinition, destinationVersion))
        //        {
        //            // ReSharper disable once AccessToDisposedClosure
        //            AssertEx.WaitUntilIsTrue(() => source.NumberOfSubscriptions > 0);

        //            var eventId = Guid.NewGuid();

        //            source.PublishEvent(eventId);

        //            // ReSharper disable once AccessToDisposedClosure
        //            AssertEx.WaitUntilIsTrue(() => destination.ReceivedEventIds.Any(ei => ei == eventId));
        //        }
        //    }
        //}

        //[Test, TestCaseSource(nameof(GenerateVersionsPairs))]
        //public void It_is_possible_to_send_and_receive_using_custom_schema_in_transport_address(string sourceVersion, string destinationVersion)
        //{
        //    sourceEndpointDefinition.Schema = SourceSchema;
        //    sourceEndpointDefinition.Mappings = new[]
        //    {
        //        new MessageMapping
        //        {
        //            MessageType = typeof(TestRequest),
        //            TransportAddress = destinationEndpointDefinition.TransportAddressForVersion(destinationVersion),
        //            Schema = DestinationSchema
        //        }
        //    };

        //    destinationEndpointDefinition.Schema = DestinationSchema;

        //    //TODO: this is a hack, passing mappings should be separate from passing schemas
        //    //if (sourceVersion.StartsWith("2") && destinationVersion.StartsWith("3"))
        //    //{
        //    //    destinationEndpointDefinition.Mappings = new[]
        //    //    {
        //    //        new MessageMapping
        //    //        {
        //    //            MessageType = typeof(TestResponse),
        //    //            TransportAddress = sourceEndpointDefinition.TransportAddressForVersion(sourceVersion) + "." + Environment.MachineName,
        //    //            Schema = SourceSchema
        //    //        }
        //    //    };
        //    //}
        //    //else
        //    {
        //        destinationEndpointDefinition.Mappings = new[]
        //        {
        //            new MessageMapping
        //            {
        //                MessageType = typeof(TestResponse),
        //                TransportAddress = sourceEndpointDefinition.TransportAddressForVersion(sourceVersion),
        //                Schema = SourceSchema
        //            }
        //        };
        //    }

        //    using (var source = EndpointFacadeBuilder.CreateAndConfigure(sourceEndpointDefinition, sourceVersion))
        //    {
        //        using (EndpointFacadeBuilder.CreateAndConfigure(destinationEndpointDefinition, destinationVersion))
        //        {
        //            var requestId = Guid.NewGuid();

        //            source.SendRequest(requestId);

        //            // ReSharper disable once AccessToDisposedClosure
        //            AssertEx.WaitUntilIsTrue(() => source.ReceivedResponseIds.Any());
        //        }
        //    }
        //}

        static object[][] GenerateVersionsPairs()
        {
            var sqlTransportVersions = new[]
            {
                "1.2",
                "2.2",
                "3.0"
            };

            var pairs = from l in sqlTransportVersions
                from r in sqlTransportVersions
                where l != r
                select new object[]
                {
                    l,
                    r
                };

            return pairs.ToArray();
        }
    }
}