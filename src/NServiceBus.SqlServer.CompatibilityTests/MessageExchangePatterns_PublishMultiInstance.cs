namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Collections.Generic;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    public partial class MessageExchangePatterns_Publish
    {
        [Test]
        public void Publish_1_2_to_2_2_on_different_instances()
        {
            Action<IEndpointConfigurationV1> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.ConfigureNamedConnectionStringForAddress(subscriber.Name, ConnectionStrings.Instance2);
            };
            Action<IEndpointConfigurationV2> subscriberConfig = c =>
            {
                var publisherAddress = $"{publisher.Name}.{Environment.MachineName}";

                c.UseConnectionString(ConnectionStrings.Instance2);
                c.UseConnectionStringForAddress(publisherAddress, ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestEvent), publisherAddress);
            };

            VerifyPublish("1.2", publisherConfig, "2.2", subscriberConfig);
        }

        [Test]
        public void Publish_1_2_to_3_0_on_different_instances()
        {
            Action<IEndpointConfigurationV1> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.ConfigureNamedConnectionStringForAddress(subscriber.Name, ConnectionStrings.Instance2);
            };
            Action<IEndpointConfigurationV3> subscriberConfig = c =>
            {
                var publisherAddress = $"{publisher.Name}.{Environment.MachineName}";

                c.UseConnectionString(ConnectionStrings.Instance1);
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    [publisherAddress] = ConnectionStrings.Instance1,
                    [""] = ConnectionStrings.Instance2 //All other addresses match here
                });
                c.RegisterPublisher(typeof(TestEvent), publisherAddress);
            };

            VerifyPublish("1.2", publisherConfig, "3.0", subscriberConfig);
        }

        [Test]
        public void Publish_2_2_to_1_2_on_different_instances()
        {
            Action<IEndpointConfigurationV2> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.UseConnectionStringForAddress($"{subscriber.Name}.{Environment.MachineName}", ConnectionStrings.Instance2);
            };
            Action<IEndpointConfigurationV1> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.ConfigureNamedConnectionStringForAddress(publisher.Name, ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestEvent), publisher.Name);
            };

            VerifyPublish("2.2", publisherConfig, "1.2", subscriberConfig);
        }

        [Test]
        public void Publish_2_2_to_3_0_on_different_instances()
        {
            Action<IEndpointConfigurationV2> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.UseConnectionStringForAddress(subscriber.Name, ConnectionStrings.Instance2);
            };
            Action<IEndpointConfigurationV3> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    [publisher.Name] = ConnectionStrings.Instance1,
                    [""] = ConnectionStrings.Instance2, //All other addresses match here
                });
                c.RegisterPublisher(typeof(TestEvent), publisher.Name);
            };

            VerifyPublish("2.2", publisherConfig, "3.0", subscriberConfig);
        }

        [Test]
        public void Publish_3_0_to_1_2_on_different_instances()
        {
            Action<IEndpointConfigurationV3> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    [$"{subscriber.Name}.{Environment.MachineName}"] = ConnectionStrings.Instance2,
                    [""] = ConnectionStrings.Instance1, //All other addresses match here
                });
            };
            Action<IEndpointConfigurationV1> destinationConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.ConfigureNamedConnectionStringForAddress(publisher.Name, ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestEvent), publisher.Name);
            };

            VerifyPublish("3.0", publisherConfig, "1.2", destinationConfig);
        }

        [Test]
        public void Publish_3_0_to_2_2_on_different_instances()
        {
            Action<IEndpointConfigurationV3> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.UseLegacyMultiInstanceMode(new Dictionary<string, string>
                {
                    [subscriber.Name] = ConnectionStrings.Instance2,
                    [""] = ConnectionStrings.Instance1, //All other addresses match here
                });
            };
            Action<IEndpointConfigurationV2> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance2);
                c.UseConnectionStringForAddress(publisher.Name, ConnectionStrings.Instance1);
                c.MapMessageToEndpoint(typeof(TestEvent), publisher.Name);
            };

            VerifyPublish("3.0", publisherConfig, "2.2", subscriberConfig);
        }
    }
}