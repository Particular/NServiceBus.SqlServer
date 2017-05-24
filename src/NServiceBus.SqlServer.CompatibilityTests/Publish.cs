namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Linq;
    using global::CompatibilityTests.Common;
    using global::CompatibilityTests.Common.Messages;
    using NUnit.Framework;

    public partial class Publish
    {
        [Test]
        public void Publish_1_2_to_2_2()
        {
            Action<IEndpointConfigurationV1> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV2> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestEvent), $"{publisher.Name}.{Environment.MachineName}");
            };

            VerifyPublish(publisherConfig, subscriberConfig);
        }

        [Test]
        public void Publish_1_2_to_3_0()
        {
            Action<IEndpointConfigurationV1> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV3> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.RegisterPublisher(typeof(TestEvent), $"{publisher.Name}.{Environment.MachineName}");
            };

            VerifyPublish(publisherConfig, subscriberConfig);
        }

        [Test]
        public void Publish_2_2_to_1_2()
        {
            Action<IEndpointConfigurationV2> publisherConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV1> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestEvent), $"{publisher.Name}");
            };

            VerifyPublish(publisherConfig, subscriberConfig);
        }

        [Test]
        public void Publish_2_2_to_3_0()
        {
            Action<IEndpointConfigurationV2> publishConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV3> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.RegisterPublisher(typeof(TestEvent), publisher.Name);
            };

            VerifyPublish(publishConfig, subscriberConfig);
        }

        [Test]
        public void Publish_3_0_to_1_2()
        {
            Action<IEndpointConfigurationV3> publishConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
            };
            Action<IEndpointConfigurationV1> subscribeConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestEvent), publisher.Name);
            };

            VerifyPublish(publishConfig, subscribeConfig);
        }

        [Test]
        public void Publish_3_0_to_2_2()
        {
            Action<IEndpointConfigurationV3> publisherConfig = c => { c.UseConnectionString(ConnectionStrings.Default); };

            Action<IEndpointConfigurationV2> subscriberConfig = c =>
            {
                c.UseConnectionString(ConnectionStrings.Default);
                c.MapMessageToEndpoint(typeof(TestEvent), publisher.Name);
            };

            VerifyPublish(publisherConfig, subscriberConfig);
        }

        [SetUp]
        public void SetUp()
        {
            publisher = new EndpointDefinition("Publisher");
            subscriber = new EndpointDefinition("Subscriber");
        }

        void VerifyPublish<S, D>(Action<S> publisherConfig, Action<D> subscriberConfig)
            where S : IEndpointConfiguration
            where D : IEndpointConfiguration
        {
            using (var publisherFacade = EndpointFacadeBuilder.CreateAndConfigure(publisher, publisherConfig))
            using (var subscriberFacade = EndpointFacadeBuilder.CreateAndConfigure(subscriber, subscriberConfig))
            {
                // ReSharper disable once AccessToDisposedClosure
                AssertEx.WaitUntilIsTrue(() => publisherFacade.NumberOfSubscriptions > 0);

                var eventId = Guid.NewGuid();

                publisherFacade.PublishEvent(eventId);

                // ReSharper disable once AccessToDisposedClosure
                AssertEx.WaitUntilIsTrue(() => subscriberFacade.ReceivedEventIds.Any(ei => ei == eventId));
            }
        }

        EndpointDefinition publisher;
        EndpointDefinition subscriber;
    }
}