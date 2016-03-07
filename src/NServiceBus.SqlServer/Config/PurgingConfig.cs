namespace NServiceBus.Transports.SQLServer.Config
{
    using System;
    using NServiceBus.Features;
    using NServiceBus.Settings;

    class PurgingConfig : ConfigBase
    {
        const string PurgeTaskDelayKey = "SqlServer.PurgeTaskDelay";
        const string PurgeBatchSizeKey = "SqlServer.PurgeBatchSize";

        public override void SetUpDefaults(SettingsHolder settings)
        {
            settings.SetDefault(PurgeTaskDelayKey, TimeSpan.FromMinutes(5));
            settings.SetDefault(PurgeBatchSizeKey, 10000);
        }

        public override void Configure(FeatureConfigurationContext context, string connectionStringWithSchema)
        {
            bool purgeOnStartup;
            if (context.Settings.TryGet("Transport.PurgeOnStartup", out purgeOnStartup) && purgeOnStartup)
            {
                context.Container.ConfigureComponent<QueuePurger>(DependencyLifecycle.SingleInstance);
            }
            else
            {
                context.Container.ConfigureComponent<NullQueuePurger>(DependencyLifecycle.SingleInstance);
            }

            var purgeParams = new PurgeExpiredMessagesParams
            {
                PurgeTaskDelay = context.Settings.Get<TimeSpan>(PurgeTaskDelayKey),
                PurgeBatchSize = context.Settings.Get<int>(PurgeBatchSizeKey)
            };
            context.Container.ConfigureComponent(() => purgeParams, DependencyLifecycle.SingleInstance);
        }
    }
}