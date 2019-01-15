namespace NServiceBus.Transport.SQLServer
{
    using Settings;

    static class DelayedDeliveryInfrastructure
    {
        public static StartupCheckResult CheckForInvalidSettings(SettingsHolder settings)
        {
            var sendOnlyEndpoint = settings.GetOrDefault<bool>("Endpoint.SendOnly");
            if (sendOnlyEndpoint)
            {
                return StartupCheckResult.Failed("Native delayed delivery is only supported for endpoints capable of receiving messages.");
            }
            return StartupCheckResult.Success;
        }
    }
}