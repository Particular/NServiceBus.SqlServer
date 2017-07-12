namespace NServiceBus.Transport.SQLServer
{
    class SettingsKeys
    {
        public const string DefaultSchemaSettingsKey = "SqlServer.SchemaName";
        public const string ConnectionFactoryOverride = "SqlServer.ConnectionFactoryOverride";
        public const string TimeToWaitBeforeTriggering = "SqlServer.CircuitBreaker.TimeToWaitBeforeTriggering";

        public const string LegacyMultiInstanceConnectionFactory = "SqlServer.LegacyMultiInstanceConnectionFactory";
        public const string MultiCatalogEnabled = "SqlServer.MultiCatalogEnabled";

        public const string PurgeTaskDelayTimeSpanKey = "SqlServer.PurgeTaskDelayTimeSpan";
        public const string PurgeBatchSizeKey = "SqlServer.PurgeBatchSize";

        public const string SchemaPropertyKey = "Schema";
        public const string CatalogPropertyKey = "Catalog";
    }
}