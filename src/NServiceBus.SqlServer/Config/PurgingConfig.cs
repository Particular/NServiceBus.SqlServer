namespace NServiceBus.Transports.SQLServer.Config
{
    using NServiceBus.Features;

    class PurgingConfig : ConfigBase
    {
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
        }
    }
}