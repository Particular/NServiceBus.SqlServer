namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using NServiceBus.Transports.SQLServer.Config;
    using NServiceBus.Transports.SQLServer.Light;
    using NUnit.Framework;


    [TestFixture]
    class CicruitBreakerConfigTests : ConfigTest
    {
        [Test]
        public void Wait_after_failure_defaults_to_10_seconds()
        {
            var busConfig = new BusConfiguration();

            var actual = new TimeSpan();
            Activate(busConfig, new CircuitBreakerConfig((error, waitBeforeTriggering, delayAfterFailure) =>
            {
                actual = delayAfterFailure;
                return null;
            }));

            Assert.AreEqual(TimeSpan.FromSeconds(10), actual);
        }

        [Test]
        public void Wait_after_failure_can_be_overridden()
        {
            var busConfig = new BusConfiguration();
            busConfig.UseTransport<SqlServerTransport>().PauseAfterReceiveFailure(TimeSpan.FromSeconds(5));

            var actual = new TimeSpan();
            Activate(busConfig, new CircuitBreakerConfig((error, waitBeforeTriggering, delayAfterFailure) =>
            {
                actual = delayAfterFailure;
                return null;
            }));

            Assert.AreEqual(TimeSpan.FromSeconds(5), actual);
        }

        [Test]
        public void Wait_before_triggering_defaults_to_2_minutes()
        {
            var busConfig = new BusConfiguration();

            var actual = new TimeSpan();
            Activate(busConfig, new CircuitBreakerConfig((error, waitBeforeTriggering, delayAfterFailure) =>
            {
                actual = waitBeforeTriggering;
                return null;
            }));

            Assert.AreEqual(TimeSpan.FromMinutes(2), actual);
        }

        [Test]
        public void Wait_before_triggering_can_be_overridden()
        {
            var busConfig = new BusConfiguration();
            busConfig.UseTransport<SqlServerTransport>().TimeToWaitBeforeTriggeringCircuitBreaker(TimeSpan.FromMinutes(4));

            var actual = new TimeSpan();
            Activate(busConfig, new CircuitBreakerConfig((error, waitBeforeTriggering, delayAfterFailure) =>
            {
                actual = waitBeforeTriggering;
                return null;
            }));

            Assert.AreEqual(TimeSpan.FromMinutes(4), actual);
        }
    }
}