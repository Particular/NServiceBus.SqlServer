namespace NServiceBus.AcceptanceTests.NativeTimeouts
{
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Features;
    using Logging;
    using NUnit.Framework;
    using Transport.SQLServer;

    public class When_configuring_delayed_delivery : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_warn_when_timeoutmanager_is_configured_without_native_delayed_delivery()
        {
            Requires.MessageDrivenPubSub();

            var context = await Scenario.Define<ScenarioContext>()
                .WithEndpoint<EndpointWithTimeoutManagerAndNotNative>()
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.True(context.EndpointsStarted, "because it should not prevent endpoint startup");

            var log = context.Logs.Single(l => l.Message.Contains("Current configuration of the endpoint uses the TimeoutManager feature for delayed delivery - an option which is not recommended for new deployments. SqlTransport native delayed delivery should be used instead. It can be enabled by calling `UseNativeDelayedDelivery()`."));
            Assert.AreEqual(LogLevel.Warn, log.Level);
        }

        [Test]
        public async Task Should_not_warn_when_timeoutmanager_and_native_delayed_delivery_are_both_configured()
        {
            Requires.MessageDrivenPubSub();

            var context = await Scenario.Define<ScenarioContext>()
                .WithEndpoint<EndpointWithTimeoutManagerAndNative>()
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.True(context.EndpointsStarted, "because it should not prevent endpoint startup");

            Assert.IsEmpty(context.Logs.Where(l => l.Message.Contains("Current configuration of the endpoint uses the TimeoutManager feature for delayed delivery - an option which is not recommended for new deployments. SqlTransport native delayed delivery should be used instead. It can be enabled by calling `UseNativeDelayedDelivery()`.")));
        }

        [Test]
        public async Task Should_not_warn_when_only_native_delayed_delivery_is_configured()
        {
            Requires.MessageDrivenPubSub();

            var context = await Scenario.Define<ScenarioContext>()
                .WithEndpoint<EndpointWithOnlyNative>()
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.True(context.EndpointsStarted, "because it should not prevent endpoint startup");

            Assert.IsEmpty(context.Logs.Where(l => l.Message.Contains("Current configuration of the endpoint uses the TimeoutManager feature for delayed delivery - an option which is not recommended for new deployments. SqlTransport native delayed delivery should be used instead. It can be enabled by calling `UseNativeDelayedDelivery()`.")));
        }

        [Test]
        public async Task Should_not_warn_when_both_native_delayed_delivery_and_timeoutmanager_is_configured_with_compatibility_disabled()
        {
            Requires.MessageDrivenPubSub();

            var context = await Scenario.Define<ScenarioContext>()
                .WithEndpoint<EndpointWithTimeoutManagerAndNativeEnabledButCompatibilityDisabled>()
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.True(context.EndpointsStarted, "because it should not prevent endpoint startup");

            Assert.IsEmpty(context.Logs.Where(l => l.Message.Contains("Current configuration of the endpoint uses the TimeoutManager feature for delayed delivery - an option which is not recommended for new deployments. SqlTransport native delayed delivery should be used instead. It can be enabled by calling `UseNativeDelayedDelivery()`.")));
        }

        public class EndpointWithTimeoutManagerAndNotNative : EndpointConfigurationBuilder
        {
            public EndpointWithTimeoutManagerAndNotNative()
            {
                EndpointSetup<DefaultServer>(config => config.EnableFeature<TimeoutManager>());
            }
        }

        public class EndpointWithTimeoutManagerAndNative : EndpointConfigurationBuilder
        {
            public EndpointWithTimeoutManagerAndNative()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.EnableFeature<TimeoutManager>();
                    config.UseTransport<SqlServerTransport>().NativeDelayedDelivery();
                });
            }
        }

        public class EndpointWithOnlyNative : EndpointConfigurationBuilder
        {
            public EndpointWithOnlyNative()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    var settings = config.UseTransport<SqlServerTransport>().NativeDelayedDelivery();
                    settings.DisableTimeoutManagerCompatibility();
                });
            }
        }

        public class EndpointWithTimeoutManagerAndNativeEnabledButCompatibilityDisabled : EndpointConfigurationBuilder
        {
            public EndpointWithTimeoutManagerAndNativeEnabledButCompatibilityDisabled()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.EnableFeature<TimeoutManager>();

                    var settings = config.UseTransport<SqlServerTransport>().NativeDelayedDelivery();
                    settings.DisableTimeoutManagerCompatibility();
                });
            }
        }
    }
}
