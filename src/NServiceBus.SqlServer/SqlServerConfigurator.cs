namespace NServiceBus.Transports.SQLServer
{
    using System.Configuration;
    using System.Linq;
    using NServiceBus.Features;
    using NServiceBus.Logging;

    class SqlServerConfigurator : Feature
    {
        SqlServerConfigurator()
        {
            //TODO: this is something that needs discussing. What happens when both msmq and sql are enabled (which will be the case probably)
            EnableByDefault();

            ThrowIvV2ConfigurationFileSettingsFound();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
        }

        private void ThrowIvV2ConfigurationFileSettingsFound()
        {
            var connectionSettings = ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().ToList();

            string message;
            var validationPassed = new ConfigurationValidator().TryValidate(connectionSettings, out message);

            if (validationPassed == false)
            {
                Logger.Error(message);

                throw new ConfigurationErrorsException(message);
            }
        }

        static ILog Logger = LogManager.GetLogger<SqlServerConfigurator>();
    }
}