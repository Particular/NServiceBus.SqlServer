namespace NServiceBus
{
    using Configuration.AdvancedExtensibility;

    /// <summary>
    /// Configuration extensions for Message-Driven Pub-Sub compatibility mode
    /// </summary>
    public static partial class MessageDrivenPubSubCompatibilityModeConfiguration
    {
        /// <summary>
        ///     Enables compatibility with endpoints running on message-driven pub-sub
        /// </summary>
        /// <param name="routingSettings">The transport to enable pub-sub compatibility on</param>
        public static SubscriptionMigrationModeSettings EnableMessageDrivenPubSubCompatibilityMode(
            this RoutingSettings routingSettings)
        {
            var settings = routingSettings.GetSettings();
            settings.Set("NServiceBus.Subscriptions.EnableMigrationMode", true);
            return new SubscriptionMigrationModeSettings(settings);
        }
    }
}