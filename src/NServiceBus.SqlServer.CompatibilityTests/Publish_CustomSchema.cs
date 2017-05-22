namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    public partial class Publish
    {
        [Test]
        public void Publish_1_2_to_2_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV1> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1_Src);
                c.ConfigureNamedConnectionStringForAddress(subscriber.Name, ConnectionStrings.Instance1_Dest);
            };
            Action<IEndpointConfigurationV2> subscriberConfig = c =>
            {
                var publisherAddress = $"{publisher.Name}.{Environment.MachineName}";

                c.DefaultSchema("dest");
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.UseSchemaForTransportAddress(publisherAddress, "src");
                c.MapMessageToEndpoint(typeof(TestEvent), publisherAddress);
            };

            VerifyPublish(publisherConfig, subscriberConfig);
        }

        [Test]
        public void Publish_1_2_to_3_0_with_custom_schemas()
        {
            Action<IEndpointConfigurationV1> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1_Src);
                c.ConfigureNamedConnectionStringForAddress(subscriber.Name, ConnectionStrings.Instance1_Dest);
            };
            Action<IEndpointConfigurationV3> subscriberConfig = c =>
            {
                var publisherAddress = $"{publisher.Name}.{Environment.MachineName}";

                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("dest");
                c.UseSchemaForQueue(publisherAddress, "src");
                c.RegisterPublisher(typeof(TestEvent), publisherAddress);
            };

            VerifyPublish(publisherConfig, subscriberConfig);
        }

        [Test]
        public void Publish_2_2_to_1_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV2> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("src");
                c.UseSchemaForTransportAddress($"{subscriber.Name}.{Environment.MachineName}", "dest");
            };
            Action<IEndpointConfigurationV1> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1_Dest);
                c.MapMessageToEndpoint(typeof(TestEvent), publisher.Name);
                c.ConfigureNamedConnectionStringForAddress(publisher.Name, ConnectionStrings.Instance1_Src);
            };

            VerifyPublish(publisherConfig, subscriberConfig);
        }

        [Test]
        public void Publish_2_2_to_3_0_with_custom_schemas()
        {
            Action<IEndpointConfigurationV2> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("src");
                c.UseSchemaForTransportAddress(subscriber.Name, "dest");
            };
            Action<IEndpointConfigurationV3> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("dest");
                c.RegisterPublisher(typeof(TestEvent), publisher.Name);
                c.UseSchemaForQueue(publisher.Name, "src");
            };

            VerifyPublish(publisherConfig, subscriberConfig);
        }

        [Test]
        public void Publish_3_0_to_1_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV3> publisherConfig = c =>
            {
                var subscriberAddress = $"{subscriber.Name}.{Environment.MachineName}";

                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("src");
                c.RouteToEndpoint(typeof(TestRequest), subscriberAddress);
                c.UseSchemaForQueue(subscriberAddress, "dest");
            };
            Action<IEndpointConfigurationV1> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1_Dest);
                c.ConfigureNamedConnectionStringForAddress(publisher.Name, ConnectionStrings.Instance1_Src);
                c.MapMessageToEndpoint(typeof(TestEvent), publisher.Name);
            };

            VerifyPublish(publisherConfig, subscriberConfig);
        }

        [Test]
        public void Publish_3_0_to_2_2_with_custom_schemas()
        {
            Action<IEndpointConfigurationV3> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("src");
                c.UseSchemaForQueue(subscriber.Name, "dest");
            };
            Action<IEndpointConfigurationV2> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema("dest");
                c.UseSchemaForTransportAddress(publisher.Name, "src");
                c.MapMessageToEndpoint(typeof(TestEvent), publisher.Name);
            };

            VerifyPublish(publisherConfig, subscriberConfig);
        }
    }
}