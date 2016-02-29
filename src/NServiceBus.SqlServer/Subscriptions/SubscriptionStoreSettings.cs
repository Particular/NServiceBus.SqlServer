namespace NServiceBus.Transports.SQLServer
{
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Settings;

    /// <summary>
    /// Allows to customize settings of subscription store.
    /// </summary>
    public class SubscriptionStoreSettings : ExposeSettings
    {
        internal SubscriptionStoreSettings(SettingsHolder settings) 
            : base(settings)
        {
        }

        /// <summary>
        /// Overrides the connection string for subscription store. 
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SubscriptionStoreSettings ConnectionString(string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);
            this.GetSettings().Set(SettingsKeys.SubscriptionStoreConnectionStringKey, connectionString);
            return this;
        }

        /// <summary>
        /// Overrides the schema for subscription store (defaults to "dbo").
        /// </summary>
        /// <param name="schemaName">Schema.</param>
        public SubscriptionStoreSettings Schema(string schemaName)
        {
            Guard.AgainstNullAndEmpty(nameof(schemaName), schemaName);
            this.GetSettings().Set(SettingsKeys.SubscriptionStoreSchemaKey, schemaName);
            return this;
        }

        /// <summary>
        /// Overrides the table name for subscription store (defaults to "Subscriptions")
        /// </summary>
        /// <param name="tableName">Table name.</param>
        public SubscriptionStoreSettings Table(string tableName)
        {
            Guard.AgainstNullAndEmpty(nameof(tableName), tableName);
            this.GetSettings().Set(SettingsKeys.SubscriptionStoreTableKey, tableName);
            return this;
        }
    }
}