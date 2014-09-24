namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;
    using SqlServerTransport = NServiceBus.SqlServerTransport;

    [TestFixture]
    public class ConfigurationTests
    {
        [Test]
        public void By_default_callbacks_are_enabled()
        {
            Configure();

            var receiveConfig = config.Builder.Build<SecondaryReceiveConfiguration>();

            Assert.IsTrue(receiveConfig.GetSettings("Endpoint").IsEnabled);
        }

        [Test]
        public void By_default_there_is_one_callback_thread()
        {
            Configure();

            var receiveConfig = config.Builder.Build<SecondaryReceiveConfiguration>();

            Assert.AreEqual(1, receiveConfig.GetSettings("Endpoint").MaximumConcurrencyLevel);
        }

        [Test]
        public void When_requested_callbacks_are_disabled()
        {
            transportExtensions.DisableCallbackReceiver();

            Configure();

            var receiveConfig = config.Builder.Build<SecondaryReceiveConfiguration>();

            Assert.IsFalse(receiveConfig.GetSettings("Endpoint").IsEnabled);
        }

        [Test]
        public void Callback_thread_count_can_be_adjusted()
        {
            transportExtensions.CallbackReceiverMaxConcurrency(7);

            Configure();

            var receiveConfig = config.Builder.Build<SecondaryReceiveConfiguration>();

            Assert.AreEqual(7, receiveConfig.GetSettings("Endpoint").MaximumConcurrencyLevel);
        }

        [Test]
        public void By_default_queue_purging_is_disabled()
        {
            Configure();

            var queuePurger = config.Builder.Build<IPurgeQueues>();

            Assert.IsInstanceOf<NullQueuePurger>(queuePurger);
        }

        [Test]
        public void Queue_purging_can_be_enabled()
        {
            busConfiguration.PurgeOnStartup(true);

            Configure();

            var queuePurger = config.Builder.Build<IPurgeQueues>();

            Assert.IsInstanceOf<QueuePurger>(queuePurger);
        }

        void Configure()
        {
            configure.Invoke(transport, new object[] { context, "ConnString" });
        }

        [SetUp]
        public void Prepare()
        {
            busConfiguration = new BusConfiguration();
            var settings = busConfiguration.GetSettings();
            settings.Set("EndpointName", "Endpoint");
            config = (Configure)buildConfiguration.Invoke(busConfiguration, new object[0]);
            context = (FeatureConfigurationContext)featureConfigContextCtor.Invoke(new object[] { config });

            transport = new Features.SqlServerTransport();
            var defaults = (List<Action<SettingsHolder>>)registeredDefaults.GetValue(transport);

            foreach (var action in defaults)
            {
                action(settings);
            }
            transportExtensions = new TransportExtensions<SqlServerTransport>(settings);
        }

        const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        static readonly MethodInfo buildConfiguration = typeof(BusConfiguration).GetMethod("BuildConfiguration", NonPublicInstance);
        static readonly ConstructorInfo featureConfigContextCtor = typeof(FeatureConfigurationContext).GetConstructor(NonPublicInstance, null, new[] { typeof(Configure) }, null);
        static readonly MethodInfo configure = typeof(Features.SqlServerTransport).GetMethod("Configure", NonPublicInstance);
        static readonly PropertyInfo registeredDefaults = typeof(Feature).GetProperty("RegisteredDefaults", NonPublicInstance);

        TransportExtensions<SqlServerTransport> transportExtensions;
        Configure config;
        Features.SqlServerTransport transport;
        FeatureConfigurationContext context;
        BusConfiguration busConfiguration;
    }
}
