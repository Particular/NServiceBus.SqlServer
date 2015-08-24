namespace NServiceBus.Transports.SQLServer.Config
{
    using NServiceBus.Features;
    using NServiceBus.Settings;

    class SqlConnectionFactoryConfig : ConfigBase
    {
        internal const string CustomSqlConnectionFactorySettingKey = "SqlServer.CustomSqlConnectionFactory";

        
        public override void SetUpDefaults(SettingsHolder settings)
        {
            settings.SetDefault(CustomSqlConnectionFactorySettingKey, ConnectionFactory.Default());
        }

        public override void Configure(FeatureConfigurationContext context, string connectionStringWithSchema)
        {
            var factory = context.Settings.Get<ConnectionFactory>(CustomSqlConnectionFactorySettingKey);
            context.Container.ConfigureComponent(b => factory, DependencyLifecycle.SingleInstance);
        }
    }
}
