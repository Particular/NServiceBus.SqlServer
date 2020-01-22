namespace NServiceBus.Transport.SQLServer
{
    class SettingsKeys
    {
        public const string DefaultSchemaSettingsKey = "SqlServer.SchemaName";
        public const string ConnectionFactoryOverride = "SqlServer.ConnectionFactoryOverride";
        public const string ConnectionProviderName = "SqlServer.ProviderName";
        public const string TimeToWaitBeforeTriggering = "SqlServer.CircuitBreaker.TimeToWaitBeforeTriggering";

        public const string LegacyMultiInstanceConnectionFactory = "SqlServer.LegacyMultiInstanceConnectionFactory";
        public const string MultiCatalogEnabled = "SqlServer.MultiCatalogEnabled";

        public const string PurgeBatchSizeKey = "SqlServer.PurgeBatchSize";
        public const string PurgeEnableKey = "SqlServer.PurgeExpiredOnStartup";

        public const string CreateMessageBodyComputedColumn = "SqlServer.CreateMessageBodyComputedColumn";

        public const string SchemaPropertyKey = "Schema";
        public const string CatalogPropertyKey = "Catalog";

        public const string TimeoutManagerMigrationMode = "NServiceBus.TimeoutManager.EnableMigrationMode";

        /// <summary>
        /// For testing the migration process only
        /// </summary>
        public const string DisableNativePubSub = "SqlServer.DisableNativePubSub";

        /// <summary>
        /// For endpoints that only consume messages e.g. ServiceControl error
        /// </summary>
        public const string DisableDelayedDelivery = "SqlServer.DisableDelayedDelivery";
    }
}