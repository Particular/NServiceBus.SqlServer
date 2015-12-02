namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
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

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure in case there are numerous errors
        /// while trying to receive messages.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="waitTime">Time to wait before triggering the circuit breaker</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan waitTime)
        {
            transportExtensions.GetSettings().Set(CircuitBreakerSettingsKeys.TimeToWaitBeforeTriggering, waitTime);
            return transportExtensions;
        }

        /// <summary>
        /// Specifies connection factory to be used by sql transport.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="sqlConnectionFactory">Factory that takes connection string and returns connection ready for usage.</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseCustomSqlConnectionFactory(this TransportExtensions<SqlServerTransport> transportExtensions, Func<string, SqlConnection> sqlConnectionFactory)
        {
            transportExtensions.GetSettings().Set(SqlServerSettingsKeys.ConnectionFactoryOverride, sqlConnectionFactory);

            return transportExtensions;
        }
    }
}