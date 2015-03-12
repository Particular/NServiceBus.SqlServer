namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;

    [TestFixture]
    public class ErrorQueueSettingsTests
    {
        [Test]
        public void It_can_try_to_obtain_the_error_queue_name_from_the_registry()
        {
            var settingsHolder = new SettingsHolder();
            settingsHolder.Set<IConfigurationSource>(new ConfigurationSource());
            settingsHolder.Set("TypesToScan", new List<Type>());
            Assert.Throws<ConfigurationErrorsException>(() => ErrorQueueSettings.GetConfiguredErrorQueue(settingsHolder));
        }

        private class ConfigurationSource : IConfigurationSource
        {
            public T GetConfiguration<T>() where T : class, new()
            {
                return null;
            }
        }
    }
}