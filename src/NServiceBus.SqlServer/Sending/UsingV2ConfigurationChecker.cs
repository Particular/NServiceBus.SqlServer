namespace NServiceBus.Transport.SQLServer
{
    using System.Configuration;
    using System.Linq;
    using Logging;
    using Transport;

    class UsingV2ConfigurationChecker
    {
        public static StartupCheckResult Check()
        {
            var connectionSettings = ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().ToList();

            var validationPassed = new ConnectionStringsValidator().TryValidate(connectionSettings, out var message);

            if (validationPassed == false)
            {
                Logger.Error(message);

                return StartupCheckResult.Failed(message);
            }

            return StartupCheckResult.Success;
        }

        static ILog Logger = LogManager.GetLogger<UsingV2ConfigurationChecker>();
    }
}