namespace NServiceBus.Transports.SQLServer
{
    using System;
    using NServiceBus.Configuration.AdvanceExtensibility;

    //TODO: let's move classes into subfolders?
    //TODO: add xml comments
    /// <summary>
    /// 
    /// </summary>
    public static partial class SqlServerTransportSettingsExtensions
    {

        /// <summary>
        /// Sets a default schema for both input and autput queues
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="schemaName"></param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> DefaultSchema(this TransportExtensions<SqlServerTransport> transportExtensions, string schemaName)
        {
            transportExtensions.GetSettings().Set(SqlServerSettingsKeys.DefaultSchemaSettingsKey, schemaName);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies callback which provides custom schema name for given table name. If null value is returned a default schema name will be used.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="schemaForQueueName">Function which takes table name and returns schema name.</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseSpecificSchema(this TransportExtensions<SqlServerTransport> transportExtensions, Func<string, string> schemaForQueueName)
        {
            transportExtensions.GetSettings().Set(SqlServerSettingsKeys.SchemaOverrideCallbackSettingsKey, schemaForQueueName);

            return transportExtensions;
        }
    }
}