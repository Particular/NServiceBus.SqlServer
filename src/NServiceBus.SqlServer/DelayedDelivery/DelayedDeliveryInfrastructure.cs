namespace NServiceBus.Transport.SQLServer
{
    using Settings;

    static class DelayedDeliveryInfrastructure
    {
        public static StartupCheckResult CheckForInvalidSettings(SettingsHolder settings)
        {
            var externalTimeoutManagerAddress = settings.GetOrDefault<string>("NServiceBus.ExternalTimeoutManagerAddress") != null;
            if (externalTimeoutManagerAddress)
            {
                return StartupCheckResult.Failed("An external timeout manager address cannot be configured because the timeout manager is not being used for delayed delivery.");
            }
            var sendOnlyEndpoint = settings.GetOrDefault<bool>("Endpoint.SendOnly");
            if (sendOnlyEndpoint)
            {
                return StartupCheckResult.Failed("Native delayed delivery is only supported for endpoints capable of receiving messages.");
            }
            return StartupCheckResult.Success;
        }
    }
}