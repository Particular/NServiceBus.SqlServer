namespace NServiceBus.Transport.SQLServer
{
    using Configuration.AdvancedExtensibility;

    /// <summary>
    /// Configuration extensions for Message-Driven Pub-Sub compatibility mode
    /// </summary>
    public static class MessageDrivenPubSubCompatibilityModeConfiguration
    {
        /// <summary>
        /// Enables compatibility with endpoints running on message-driven pub-sub
        /// </summary>
        /// <param name="transportExtensions">The transport to enable pub-sub compatibility on</param>
        public static SubscriptionMigrationModeSettings EnableMessageDrivenPubSubCompatibilityMode(this TransportExtensions<SqlServerTransport> transportExtensions)
        {
            var settings = transportExtensions.GetSettings();
            settings.Set("NServiceBus.Subscriptions.EnableMigrationMode", true);
            settings.Set("SqlServer.Subscriptions.EnableMigrationMode", true);
            return new SubscriptionMigrationModeSettings(settings);
        }
    }
}