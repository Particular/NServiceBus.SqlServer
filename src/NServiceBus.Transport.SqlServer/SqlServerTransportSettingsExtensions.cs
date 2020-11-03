namespace NServiceBus
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using System.Transactions;
    using Configuration.AdvancedExtensibility;
    using Logging;
    using Transport.SqlServer;

    /// <summary>
    /// Adds extra configuration for the Sql Server transport.
    /// </summary>
    public static class SqlServerTransportSettingsExtensions
    {
        /// <summary>
        /// Sets a default schema for both input and output queues
        /// </summary>
        public static TransportExtensions<SqlServerTransport> DefaultSchema(this TransportExtensions<SqlServerTransport> transportExtensions, string schemaName)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNullAndEmpty(nameof(schemaName), schemaName);

            transportExtensions.GetSettings().Set(SettingsKeys.DefaultSchemaSettingsKey, schemaName);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies custom schema for given endpoint.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="endpointName">Endpoint name.</param>
        /// <param name="schema">Custom schema value.</param>
        public static TransportExtensions<SqlServerTransport> UseSchemaForEndpoint(this TransportExtensions<SqlServerTransport> transportExtensions, string endpointName, string schema)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNullAndEmpty(nameof(endpointName), endpointName);
            Guard.AgainstNullAndEmpty(nameof(schema), schema);

            var schemasConfiguration = transportExtensions.GetSettings().GetOrCreate<EndpointSchemaAndCatalogSettings>();

            schemasConfiguration.SpecifySchema(endpointName, schema);

            return transportExtensions;
        }

        /// <summary>
        /// Overrides schema value for given queue. This setting will take precedence over any other source of schema information.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="queueName">Queue name.</param>
        /// <param name="schema">Custom schema value.</param>
        public static TransportExtensions<SqlServerTransport> UseSchemaForQueue(this TransportExtensions<SqlServerTransport> transportExtensions, string queueName, string schema)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNullAndEmpty(nameof(queueName), queueName);
            Guard.AgainstNullAndEmpty(nameof(schema), schema);

            var schemasConfiguration = transportExtensions.GetSettings().GetOrCreate<QueueSchemaAndCatalogSettings>();

            schemasConfiguration.SpecifySchema(queueName, schema);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies custom schema for given endpoint.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="endpointName">Endpoint name.</param>
        /// <param name="catalog">Custom catalog value.</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseCatalogForEndpoint(this TransportExtensions<SqlServerTransport> transportExtensions, string endpointName, string catalog)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNullAndEmpty(nameof(endpointName), endpointName);
            Guard.AgainstNullAndEmpty(nameof(catalog), catalog);

            var settings = transportExtensions.GetSettings();

            settings.Set(SettingsKeys.MultiCatalogEnabled, true);

            var schemasConfiguration = settings.GetOrCreate<EndpointSchemaAndCatalogSettings>();

            schemasConfiguration.SpecifyCatalog(endpointName, catalog);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies custom schema for given queue.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="queueName">Queue name.</param>
        /// <param name="catalog">Custom catalog value.</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseCatalogForQueue(this TransportExtensions<SqlServerTransport> transportExtensions, string queueName, string catalog)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNullAndEmpty(nameof(queueName), queueName);
            Guard.AgainstNullAndEmpty(nameof(catalog), catalog);

            var settings = transportExtensions.GetSettings();

            settings.Set(SettingsKeys.MultiCatalogEnabled, true);

            var schemasConfiguration = settings.GetOrCreate<QueueSchemaAndCatalogSettings>();

            schemasConfiguration.SpecifyCatalog(queueName, catalog);

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
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNegativeAndZero(nameof(waitTime), waitTime);

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
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNull(nameof(sqlConnectionFactory), sqlConnectionFactory);

            transportExtensions.GetSettings().Set(SettingsKeys.ConnectionFactoryOverride, sqlConnectionFactory);

            return transportExtensions;
        }

        /// <summary>
        /// Allows the <see cref="IsolationLevel"/> and transaction timeout to be changed for the <see cref="TransactionScope"/> used to receive messages.
        /// </summary>
        /// <remarks>
        /// If not specified the default transaction timeout of the machine will be used and the isolation level will be set to
        /// <see cref="IsolationLevel.ReadCommitted"/>.
        /// </remarks>
        public static TransportExtensions<SqlServerTransport> TransactionScopeOptions(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan? timeout = null, IsolationLevel? isolationLevel = null)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            if (isolationLevel != IsolationLevel.ReadCommitted && isolationLevel != IsolationLevel.RepeatableRead)
            {
                Logger.Warn("TransactionScope should be only used with either the ReadCommitted or the RepeatableRead isolation level.");
            }

            transportExtensions.GetSettings().Set(new SqlScopeOptions(timeout, isolationLevel));

            return transportExtensions;
        }

        /// <summary>
        /// Allows changing the queue peek delay.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="delay">The delay value</param>
        [ObsoleteEx(Message = "WithPeekDelay has been obsoleted.", ReplacementTypeOrMember = "QueuePeekerOptions", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
        public static TransportExtensions<SqlServerTransport> WithPeekDelay(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan? delay = null)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.QueuePeekerOptions(delay: delay);
            return transportExtensions;
        }

        /// <summary>
        /// Allows changing the queue peek delay, and the paeek batch size.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="delay">The delay value</param>
        /// <param name="peekBatchSize">The peek batch size</param>
        public static TransportExtensions<SqlServerTransport> QueuePeekerOptions(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan? delay = null, int? peekBatchSize = null)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.GetSettings().Set(new QueuePeekerOptions(delay, peekBatchSize));
            return transportExtensions;
        }

        /// <summary>
        /// Configures native delayed delivery.
        /// </summary>
        public static DelayedDeliverySettings NativeDelayedDelivery(this TransportExtensions<SqlServerTransport> transportExtensions)
        {
            var sendOnlyEndpoint = transportExtensions.GetSettings().GetOrDefault<bool>("Endpoint.SendOnly");
            if (sendOnlyEndpoint)
            {
                throw new Exception("Delayed delivery is only supported for endpoints capable of receiving messages.");
            }

            return new DelayedDeliverySettings(transportExtensions.GetSettings());
        }

        /// <summary>
        /// Configures publish/subscribe behavior.
        /// </summary>
        public static SubscriptionSettings SubscriptionSettings(this TransportExtensions<SqlServerTransport> transportExtensions)
        {
            return transportExtensions.GetSettings().GetOrCreate<SubscriptionSettings>();
        }

        /// <summary>
        /// Instructs the transport to purge all expired messages from the input queue before starting the processing.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="purgeBatchSize">Maximum number of messages in each delete batch.</param>
        public static TransportExtensions<SqlServerTransport> PurgeExpiredMessagesOnStartup(this TransportExtensions<SqlServerTransport> transportExtensions, int? purgeBatchSize)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.PurgeEnableKey, true);
            if (purgeBatchSize.HasValue)
            {
                transportExtensions.GetSettings().Set(SettingsKeys.PurgeBatchSizeKey, purgeBatchSize);
            }
            return transportExtensions;
        }

        /// <summary>
        /// Instructs the transport to create a computed column for inspecting message body contents.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        public static TransportExtensions<SqlServerTransport> CreateMessageBodyComputedColumn(this TransportExtensions<SqlServerTransport> transportExtensions)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.CreateMessageBodyComputedColumn, true);

            return transportExtensions;
        }

        static ILog Logger = LogManager.GetLogger<SqlServerTransport>();
    }
}