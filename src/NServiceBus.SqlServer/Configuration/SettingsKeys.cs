
namespace NServiceBus.Transports.SQLServer
{
    class SettingsKeys
    {
        public const string DefaultSchemaSettingsKey = "SqlServer.SchemaName";
        public const string SchemaOverrideCallbackSettingsKey = "SqlServer.ScheamOverride.Callback";
        public const string ConnectionFactoryOverride = "SqlServer.ConnectionFactoryOverride";
        public const string TimeToWaitBeforeTriggering = "SqlServer.CircuitBreaker.TimeToWaitBeforeTriggering";

        public const string LegacyMultiInstanceConnectionFactory = "SqlServer.LegacyMultiInstanceConnectionFactory";

        public const string PurgeTaskDelayKey = "SqlServer.PurgeTaskDelay";
        public const string PurgeBatchSizeKey = "SqlServer.PurgeBatchSize";
    }
}