namespace NServiceBus.Transport.SqlServer
{
    class SettingsKeys
    {
        public const string DefaultSchemaSettingsKey = "SqlServer.SchemaName";
        public const string DefaultCatalogSettingsKey = "SqlServer.CatalogName";

        public const string ConnectionFactoryOverride = "SqlServer.ConnectionFactoryOverride";
        public const string TimeToWaitBeforeTriggering = "SqlServer.CircuitBreaker.TimeToWaitBeforeTriggering";

        public const string LegacyMultiInstanceConnectionFactory = "SqlServer.LegacyMultiInstanceConnectionFactory";
        public const string MultiCatalogEnabled = "SqlServer.MultiCatalogEnabled";

        public const string PurgeBatchSizeKey = "SqlServer.PurgeBatchSize";
        public const string PurgeEnableKey = "SqlServer.PurgeExpiredOnStartup";

        public const string CreateMessageBodyComputedColumn = "SqlServer.CreateMessageBodyComputedColumn";

        public const string SchemaPropertyKey = "Schema";
        public const string CatalogPropertyKey = "Catalog";

        public const string TimeoutManagerMigrationMode = "NServiceBus.TimeoutManager.EnableMigrationMode";

        public const string DelayedDeliverySuffix = "SqlServer.DelayedDeliverySuffix";
        public const string DelayedDeliveryInterval = "SqlServer.DelayedDeliveryInterval";
        public const string DelayedDeliveryMatureBatchSize = "SqlServer.DelayedDeliveryMatureBatchSize";

        /// <summary>
        /// For testing the migration process only
        /// </summary>
        public const string DisableNativePubSub = "SqlServer.DisableNativePubSub";
        public const string SubscriptionTableQuotedQualifiedNameSetter = "SqlServer.SubscriptionTableQuotedQualifiedNameSetter";

        /// <summary>
        /// For endpoints that only consume messages e.g. ServiceControl error
        /// </summary>
        public const string DisableDelayedDelivery = "SqlServer.DisableDelayedDelivery";

        // For backward compatibility reasons these settings keys are hard coded to the System.Data types to enable connection and transaction sharing with SQL persistence
        public const string TransportTransactionSqlConnectionKey = "System.Data.SqlClient.SqlConnection";
        public const string TransportTransactionSqlTransactionKey = "System.Data.SqlClient.SqlTransaction";

        public const string IsUserProvidedTransactionKey = "SqlServer.Transaction.IsUserProvided";
    }
}