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

                c.DefaultSchema(ConnectionStrings.Schema_Dest);
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.UseSchemaForTransportAddress(publisherAddress, ConnectionStrings.Schema_Src);
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
                c.DefaultSchema(ConnectionStrings.Schema_Dest);
                c.UseSchemaForQueue(publisherAddress, ConnectionStrings.Schema_Src);
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
                c.DefaultSchema(ConnectionStrings.Schema_Src);
                c.UseSchemaForTransportAddress($"{subscriber.Name}.{Environment.MachineName}", ConnectionStrings.Schema_Dest);
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
                c.DefaultSchema(ConnectionStrings.Schema_Src);
                c.UseSchemaForTransportAddress(subscriber.Name, ConnectionStrings.Schema_Dest);
            };
            Action<IEndpointConfigurationV3> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema(ConnectionStrings.Schema_Dest);
                c.RegisterPublisher(typeof(TestEvent), publisher.Name);
                c.UseSchemaForQueue(publisher.Name, ConnectionStrings.Schema_Src);
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
                c.DefaultSchema(ConnectionStrings.Schema_Src);
                c.RouteToEndpoint(typeof(TestRequest), subscriberAddress);
                c.UseSchemaForQueue(subscriberAddress, ConnectionStrings.Schema_Dest);
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
                c.DefaultSchema(ConnectionStrings.Schema_Src);
                c.UseSchemaForQueue(subscriber.Name, ConnectionStrings.Schema_Dest);
            };
            Action<IEndpointConfigurationV2> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Instance1);
                c.DefaultSchema(ConnectionStrings.Schema_Dest);
                c.UseSchemaForTransportAddress(publisher.Name, ConnectionStrings.Schema_Src);
                c.MapMessageToEndpoint(typeof(TestEvent), publisher.Name);
            };

            VerifyPublish(publisherConfig, subscriberConfig);
        }
    }
}