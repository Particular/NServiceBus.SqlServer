namespace NServiceBus.Transports.SQLServer
{
    using System.Configuration;
    using System.Linq;
    using NServiceBus.Logging;

    class UsingOldConfigurationCheck
    {
        public static StartupCheckResult Check()
        {
            var connectionSettings = ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().ToList();

            string message;
            var validationPassed = new ConnectionStringsValidator().TryValidate(connectionSettings, out message);

            if (validationPassed == false)
            {
                Logger.Error(message);

                return StartupCheckResult.Failed(message);
            }

            return StartupCheckResult.Success;
        }

        static ILog Logger = LogManager.GetLogger<UsingOldConfigurationCheck>();
    }
}
