namespace NServiceBus.SqlServer.UnitTests
{
    using System.Linq;
    using NServiceBus.Features;
    using NServiceBus.Support;
    using NServiceBus.Transports.SQLServer;
    using NServiceBus.Transports.SQLServer.Config;
    using NUnit.Framework;

    abstract class CallbackConfigTests
    {
        [TestFixture]
        class When_callbacks_are_disabled : ConfigTest
        {
            [Test]
            public void Header_rewrite_behavior_is_not_registered()
            {
                var busConfig = new BusConfiguration();
                busConfig.UseTransport<SqlServerTransport>().DisableCallbackReceiver();

                var builder = Activate(busConfig, new CallbackConfig());

                Assert.IsNull(builder.Build<SetOutgoingCallbackAddressBehavior>());
            }
            
            [Test]
            public void Receive_is_disabled_for_main_queue()
            {
                var busConfig = new BusConfiguration();
                busConfig.EndpointName("Endpoint");
                busConfig.UseTransport<SqlServerTransport>().DisableCallbackReceiver();

                var builder = Activate(busConfig, new CallbackConfig());

                var receiveConfig = builder.Build<SecondaryReceiveConfiguration>();
                var settings = receiveConfig.GetSettings("Endpoint");

                Assert.IsFalse(settings.IsEnabled);
            }
        }

        [TestFixture]
        class When_callbacks_are_enabled : ConfigTest
        {
            [Test]
            public void Receive_is_enabled_for_main_queue()
            {
                var busConfig = new BusConfiguration();
                busConfig.EndpointName("Endpoint");

                var builder = Activate(busConfig, new CallbackConfig());

                var receiveConfig = builder.Build<SecondaryReceiveConfiguration>();
                var settings = receiveConfig.GetSettings("Endpoint");

                Assert.IsTrue(settings.IsEnabled);
            }
            
            [Test]
            public void Receive_is_disabled_for_satellite_queues()
            {
                var busConfig = new BusConfiguration();
                busConfig.EndpointName("Endpoint");

                var builder = Activate(busConfig, new CallbackConfig());

                var receiveConfig = builder.Build<SecondaryReceiveConfiguration>();
                var settings = receiveConfig.GetSettings("Endpoint.Satellite");

                Assert.IsFalse(settings.IsEnabled);
            }
        }
    }
}