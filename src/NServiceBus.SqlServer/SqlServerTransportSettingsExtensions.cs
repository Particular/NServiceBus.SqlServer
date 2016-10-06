namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using Configuration.AdvanceExtensibility;

    /// <summary>
    /// Adds extra configuration for the Sql Server transport.
    /// </summary>
    public static partial class SqlServerTransportSettingsExtensions
    {
        /// <summary>
        /// Sets a default schema for both input and output queues
        /// </summary>
        public static TransportExtensions<SqlServerTransport> DefaultSchema(this TransportExtensions<SqlServerTransport> transportExtensions, string schemaName)
        {
            Guard.AgainstNullAndEmpty("schemaName", schemaName);

            transportExtensions.GetSettings().Set(SettingsKeys.DefaultSchemaSettingsKey, schemaName);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies custom schema for given endpoint.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="endpointName">Endpoint name.</param>
        /// <param name="schema">Custom schema value.</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseSchemaForEndpoint(this TransportExtensions<SqlServerTransport> transportExtensions, string endpointName, string schema)
        {
            Guard.AgainstNullAndEmpty(nameof(endpointName), endpointName);
            Guard.AgainstNullAndEmpty(nameof(schema), schema);

            var schemasConfiguration = transportExtensions.GetSettings().GetOrCreate<EndpointSchemasSettings>();

            schemasConfiguration.AddOrUpdate(endpointName, schema);

            return transportExtensions;
        }

        /// <summary>
        /// Overrides schema value for given queue. This setting will take precedence over any other source of schema information.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="queueName">Queue name.</param>
        /// <param name="schema">Custom schema value.</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseSchemaForQueue(this TransportExtensions<SqlServerTransport> transportExtensions, string queueName, string schema)
        {
            Guard.AgainstNullAndEmpty(nameof(queueName), queueName);
            Guard.AgainstNullAndEmpty(nameof(schema), schema);

            var schemasConfiguration = transportExtensions.GetSettings().GetOrCreate<TableSchemasSettings>();

            schemasConfiguration.AddOrUpdate(queueName, schema);

            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure
        /// in case there are numerous errors
        /// while trying to receive messages.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="waitTime">Time to wait before triggering the circuit breaker.</param>
        public static TransportExtensions<SqlServerTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan waitTime)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.TimeToWaitBeforeTriggering, waitTime);
            return transportExtensions;
        }

        /// <summary>
        /// Specifies connection factory to be used by sql transport.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="sqlConnectionFactory">Factory that returns connection ready for usage.</param>
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
        /// If not specified the default transaction timeout of the machine will be used and the isolation level will be set to
        /// `ReadCommited`.
        /// </remarks>
        public static TransportExtensions<SqlServerTransport> TransactionScopeOptions(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan? timeout = null, IsolationLevel? isolationLevel = null)
        {
            transportExtensions.GetSettings().Set<SqlScopeOptions>(new SqlScopeOptions(timeout, isolationLevel));
            return transportExtensions;
        }

        /// <summary>
        /// Allows changing the queue peek delay.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="delay">The delay value</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> WithPeekDelay(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan? delay = null)
        {
            transportExtensions.GetSettings().Set<QueuePeekerOptions>(new QueuePeekerOptions(delay));
            return transportExtensions;
        }

        /// <summary>
        /// Changes the default suffix of delayed message table (Delayed).
        /// </summary>
        public static DelayedDeliverySettings EnableNativeDelayedMessageDelivery(this TransportExtensions<SqlServerTransport> transportExtensions)
        {
            var settings = new DelayedDeliverySettings(transportExtensions.GetSettings());
            transportExtensions.GetSettings().Set<DelayedDeliverySettings>(settings);
            return settings;
        }
        
        /// <summary>
        /// Enables multi-instance mode.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="sqlConnectionFactory">Function that returns opened sql connection based on destination queue name.</param>
        [ObsoleteEx(RemoveInVersion = "4.0", TreatAsErrorFromVersion = "4.0", Message = "Multi-instance mode has been deprecated and is no longer a recommended model of deployment. Please refer to documentation for more details.")]
        public static TransportExtensions<SqlServerTransport> EnableLegacyMultiInstanceMode(this TransportExtensions<SqlServerTransport> transportExtensions, Func<string, Task<SqlConnection>> sqlConnectionFactory)
        {
            Guard.AgainstNull(nameof(sqlConnectionFactory), sqlConnectionFactory);

            transportExtensions.GetSettings().Set(SettingsKeys.LegacyMultiInstanceConnectionFactory, sqlConnectionFactory);

            return transportExtensions;
        }
    }
}