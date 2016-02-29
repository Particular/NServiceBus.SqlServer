namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Configuration.AdvanceExtensibility;

    /// <summary>
    /// Adds extra configuration for the Sql Server transport.
    /// </summary>
    public static partial class SqlServerTransportSettingsExtensions
    {

        /// <summary>
        /// Sets a default schema for both input and output queues
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="schemaName"></param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> DefaultSchema(this TransportExtensions<SqlServerTransport> transportExtensions, string schemaName)
        {
            Guard.AgainstNullAndEmpty("schemaName", schemaName);

            transportExtensions.GetSettings().Set(SettingsKeys.DefaultSchemaSettingsKey, schemaName);

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
            Guard.AgainstNull("schemaForQueueName", schemaForQueueName);

            transportExtensions.GetSettings().Set(SettingsKeys.SchemaOverrideCallbackSettingsKey, schemaForQueueName);

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
            transportExtensions.GetSettings().Set(SettingsKeys.TimeToWaitBeforeTriggering, waitTime);
            return transportExtensions;
        }

        /// <summary>
        /// Specifies connection factory to be used by sql transport.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="sqlConnectionFactory">Factory that returns connection ready for usage.</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseCustomSqlConnectionFactory(this TransportExtensions<SqlServerTransport> transportExtensions, Func<Task<SqlConnection>> sqlConnectionFactory)
        {
            Guard.AgainstNull("sqlConnectionFactory", sqlConnectionFactory);

            transportExtensions.GetSettings().Set(SettingsKeys.ConnectionFactoryOverride, sqlConnectionFactory);

            return transportExtensions;
        }

        /// <summary>
        /// Allows the IsolationLevel and transaction timeout to be changed for the TransactionScope used to receive messages.
        /// </summary>
        /// <remarks>
        /// If not specified the default transaction timeout of the machine will be used and the isolation level will be set to `ReadCommited`.
        /// </remarks>
        public static TransportExtensions<SqlServerTransport> TransactionScopeOptions(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan? timeout = null, IsolationLevel? isolationLevel = null)
        {
            transportExtensions.GetSettings().Set<SqlScopeOptions>(new SqlScopeOptions(timeout, isolationLevel));
            return transportExtensions;
        }

        /// <summary>
        /// Enables legacy multi-instance mode. 
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="sqlConnectionFactory">Function that returns opened sql connection based on destination transport address..</param>
        /// <returns></returns>
        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "4.0", Message = "Multi-instance mode will be removed in future versions of SqlServer transport.")]
        public static TransportExtensions<SqlServerTransport> EnableLagacyMultiInstanceMode(this TransportExtensions<SqlServerTransport> transportExtensions, Func<string, Task<SqlConnection>> sqlConnectionFactory)
        {
            Guard.AgainstNull(nameof(sqlConnectionFactory), sqlConnectionFactory);

            transportExtensions.GetSettings().Set(SettingsKeys.LegacyMultiInstanceConnectionFactory, sqlConnectionFactory);

            return transportExtensions;
        }

        /// <summary>
        /// Allows to customize subscription store settings.
        /// </summary>
        public static SubscriptionStoreSettings CustomizeSubscriptionStore(this TransportExtensions<SqlServerTransport> transportExtensions)
        {
            return new SubscriptionStoreSettings(transportExtensions.GetSettings());
        }
    }
}