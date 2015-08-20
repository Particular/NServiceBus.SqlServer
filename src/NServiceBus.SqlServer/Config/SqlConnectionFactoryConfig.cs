namespace NServiceBus.Transports.SQLServer.Config
{
    using System;
    using System.Data.SqlClient;
    using NServiceBus.Features;
    using NServiceBus.Settings;

    class SqlConnectionFactoryConfig : ConfigBase
    {
        internal const string CustomSqlConnectionFactorySettingKey = "SqlServer.CustomSqlConnectionFactory";

        static SqlConnection DefaultOpenNewConnection(string connectionString)
        {
            var connection = new SqlConnection(connectionString);

            try
            {
                connection.Open();
            }
            catch (Exception)
            {
                connection.Dispose();
                throw;
            }

            return connection;
        }

        public override void SetUpDefaults(SettingsHolder settings)
        {
            settings.SetDefault(CustomSqlConnectionFactorySettingKey, new CustomSqlConnectionFactory(DefaultOpenNewConnection));
        }

        public override void Configure(FeatureConfigurationContext context, string connectionStringWithSchema)
        {
            var factory = (CustomSqlConnectionFactory)context.Settings.Get(CustomSqlConnectionFactorySettingKey);
            context.Container.ConfigureComponent(b => factory, DependencyLifecycle.SingleInstance);
        }
    }
}
