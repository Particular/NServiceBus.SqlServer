namespace NServiceBus.SqlServer.UnitTests
{
    using System.Linq;
    using NServiceBus.Features;
    using NServiceBus.Support;
    using NServiceBus.Transports.SQLServer;
    using NServiceBus.Transports.SQLServer.Config;
    using NServiceBus.Transports.SQLServer.Light;
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

            [Test]
            public void Header_reading_behavior_is_registered()
            {
                var busConfig = new BusConfiguration();

                Activate(busConfig, new CallbackConfig());

                Assert.IsTrue(PipelineExecutor.Incoming.Any(x => x.BehaviorType == typeof(ReadIncomingCallbackAddressBehavior)));
            }
        }

        [TestFixture]
        class When_callbacks_are_enabled : ConfigTest
        {
            [Test]
            public void Header_setting_behavior_is_registered()
            {
                var busConfig = new BusConfiguration();

                Activate(busConfig, new CallbackConfig());

                Assert.IsTrue(PipelineExecutor.Outgoing.Any(x => x.BehaviorType == typeof(SetOutgoingCallbackAddressBehavior)));
            }

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
            public void Receive_concurrency_defaults_to_1()
            {
                var busConfig = new BusConfiguration();
                busConfig.EndpointName("Endpoint");

                var builder = Activate(busConfig, new CallbackConfig());

                var receiveConfig = builder.Build<SecondaryReceiveConfiguration>();
                var settings = receiveConfig.GetSettings("Endpoint");

                Assert.AreEqual(1, settings.MaximumConcurrencyLevel);
            }
            
            [Test]
            public void Receive_concurrency_can_be_overridden()
            {
                var busConfig = new BusConfiguration();
                busConfig.EndpointName("Endpoint");
                busConfig.UseTransport<SqlServerTransport>().CallbackReceiverMaxConcurrency(9);

                var builder = Activate(busConfig, new CallbackConfig());

                var receiveConfig = builder.Build<SecondaryReceiveConfiguration>();
                var settings = receiveConfig.GetSettings("Endpoint");

                Assert.AreEqual(9, settings.MaximumConcurrencyLevel);
            }

            [Test]
            public void Callback_queue_name_contains_machine_name()
            {
                RuntimeEnvironment.MachineNameAction = () => "MyMachine";

                var busConfig = new BusConfiguration();
                busConfig.EndpointName("Endpoint");
                busConfig.UseTransport<SqlServerTransport>().CallbackReceiverMaxConcurrency(9);

                var builder = Activate(busConfig, new CallbackConfig());

                var receiveConfig = builder.Build<SecondaryReceiveConfiguration>();
                var settings = receiveConfig.GetSettings("Endpoint");

                Assert.AreEqual("Endpoint.MyMachine", settings.ReceiveQueue);
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