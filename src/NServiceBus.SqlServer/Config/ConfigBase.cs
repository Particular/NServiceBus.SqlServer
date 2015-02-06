namespace NServiceBus.Transports.SQLServer.Config
{
    using NServiceBus.Features;
    using NServiceBus.Settings;

    abstract class ConfigBase
    {
        public virtual void SetUpDefaults(SettingsHolder settings)
        {
        }

        public abstract void Configure(FeatureConfigurationContext context, string connectionStringWithSchema);
    }
}