namespace NServiceBus.Transport.SqlServer.Configuration
{
    using System;
    using System.Threading.Tasks;
    using System.Transactions;
    using Microsoft.Data.SqlClient;

    /// <summary>
    /// Provides support for <see cref="UseTransport{T}"/> transport APIs.
    /// </summary>
    public static class SqlServerTransportApiExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>
        [PreObsolete(
            RemoveInVersion = "10",
            TreatAsErrorFromVersion = "9",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static TransportExtensions<SqlServerTransport> UseTransport<T>(this EndpointConfiguration config)
            where T : SqlServerTransport
        {
            var transport = new SqlServerTransport();

            var routing = config.UseTransport(transport);

            var settings = new TransportExtensions<SqlServerTransport>(transport, routing);

            return settings;
        }

        /// <summary>
        ///     Sets a default schema for both input and output queues
        /// </summary>
        [PreObsolete(Message = "DefaultSchema has been obsoleted.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9",
            ReplacementTypeOrMember = "SqlServerTransport.DefaultSchema")]
        public static TransportExtensions<SqlServerTransport> DefaultSchema(
            this TransportExtensions<SqlServerTransport> transport, string schemaName)
        {
            transport.Transport.DefaultSchema = schemaName;

            return transport;
        }

        /// <summary>
        ///     Sets a default schema for both input and output queues
        /// </summary>
        [PreObsolete(Message = "DefaultCatalog has been obsoleted.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9",
            ReplacementTypeOrMember = "SqlServerTransport.DefaultCatalog")]
        public static TransportExtensions<SqlServerTransport> DefaultCatalog(
            this TransportExtensions<SqlServerTransport> transport, string catalogName)
        {
            transport.Transport.DefaultCatalog = catalogName;

            return transport;
        }

        /// <summary>
        ///     Specifies custom schema for given endpoint.
        /// </summary>
        /// <param name="endpointName">Endpoint name.</param>
        /// <param name="schema">Custom schema value.</param>
        /// <param name="transport">The transport settings to configure.</param>
        [ObsoleteEx(Message = "UseSchemaForEndpoint has been obsoleted.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "9",
            ReplacementTypeOrMember = "RoutingSettings.UseSchemaForEndpoint")]
        public static TransportExtensions<SqlServerTransport> UseSchemaForEndpoint(
            this TransportExtensions<SqlServerTransport> transport, string endpointName, string schema) =>
            throw new NotImplementedException();

        /// <summary>
        /// Overrides schema value for given queue. This setting will take precedence over any other source of schema
        /// information.
        /// </summary>
        /// <param name="queueName">Queue name.</param>
        /// <param name="schema">Custom schema value.</param>
        /// <param name="transport">The transport settings to configure.</param>
        [PreObsolete(Message = "UseSchemaForQueue has been obsoleted.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9",
            ReplacementTypeOrMember = "SqlServerTransport.SchemaAndCatalog.UseSchemaForQueue")]
        public static TransportExtensions<SqlServerTransport> UseSchemaForQueue(
            this TransportExtensions<SqlServerTransport> transport, string queueName, string schema)
        {
            transport.Transport.SchemaAndCatalog.UseSchemaForQueue(queueName, schema);

            return transport;
        }

        /// <summary>
        ///  Specifies custom schema for given endpoint.
        /// </summary>
        /// <param name="endpointName">Endpoint name.</param>
        /// <param name="catalog">Custom catalog value.</param>
        /// <param name="transport">The transport settings to configure.</param>
        [ObsoleteEx(Message = "UseCatalogForEndpoint has been obsoleted.",
            TreatAsErrorFromVersion = "7.0",
            RemoveInVersion = "8.0",
            ReplacementTypeOrMember = "RoutingSettings.UseCatalogForEndpoint")]
        public static TransportExtensions<SqlServerTransport> UseCatalogForEndpoint(
            this TransportExtensions<SqlServerTransport> transport, string endpointName, string catalog) =>
            throw new NotImplementedException();

        /// <summary>
        /// Specifies custom schema for given queue.
        /// </summary>
        /// <param name="queueName">Queue name.</param>
        /// <param name="catalog">Custom catalog value.</param>
        /// <param name="transport">The transport settings to configure.</param>
        [ObsoleteEx(Message = "UseCatalogForQueue has been obsoleted.",
            TreatAsErrorFromVersion = "8.0",
            RemoveInVersion = "9.0",
            ReplacementTypeOrMember = "SqlServerTransport.SchemaAndCatalog.UseCatalogForQueue")]
        public static TransportExtensions<SqlServerTransport> UseCatalogForQueue(
            this TransportExtensions<SqlServerTransport> transport, string queueName, string catalog)
        {
            transport.Transport.SchemaAndCatalog.UseCatalogForQueue(queueName, catalog);

            return transport;
        }

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown
        /// procedure in case there are numerous errors while trying to receive messages.
        /// </summary>
        /// <param name="waitTime">Time to wait before triggering the circuit breaker.</param>
        /// <param name="transport">The transport settings to configure.</param>
        [PreObsolete(Message = "TimeToWaitBeforeTriggeringCircuitBreaker has been obsoleted.",
            TreatAsErrorFromVersion = "8.0",
            RemoveInVersion = "9.0",
            ReplacementTypeOrMember = "SqlServerTransport.TimeToWaitBeforeTriggeringCircuitBreaker")]
        public static TransportExtensions<SqlServerTransport> TimeToWaitBeforeTriggeringCircuitBreaker(
            this TransportExtensions<SqlServerTransport> transport, TimeSpan waitTime)
        {
            transport.Transport.TimeToWaitBeforeTriggeringCircuitBreaker = waitTime;

            return transport;
        }

        /// <summary>
        /// Specifies connection factory to be used by sql transport.
        /// </summary>
        /// <param name="connectionString">Sql Server instance connection string.</param>
        /// <param name="transport">The transport settings to configure.</param>
        [PreObsolete(Message = "ConnectionString has been obsoleted.",
            ReplacementTypeOrMember = "configuration.UseTransport(new SqlServerTransport(string connectionString))",
            RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
        public static TransportExtensions<SqlServerTransport> ConnectionString(
            this TransportExtensions<SqlServerTransport> transport, string connectionString)
        {
            transport.Transport.ConnectionString = connectionString;

            return transport;
        }

        /// <summary>
        /// Specifies connection factory to be used by sql transport.
        /// </summary>
        /// <param name="sqlConnectionFactory">Factory that returns connection ready for usage.</param>
        /// <param name="transport">The transport settings to configure.</param>
        [PreObsolete(Message = "UseCustomSqlConnectionFactory has been obsoleted.",
            TreatAsErrorFromVersion = "8.0",
            RemoveInVersion = "9.0",
            ReplacementTypeOrMember = "SqlServerTransport.ConnectionFactory")]
        public static TransportExtensions<SqlServerTransport> UseCustomSqlConnectionFactory(
            this TransportExtensions<SqlServerTransport> transport,
            Func<Task<SqlConnection>> sqlConnectionFactory)
        {
            transport.Transport.ConnectionFactory = async (_) => await sqlConnectionFactory().ConfigureAwait(false);

            return transport;
        }

        /// <summary>
        /// Allows the <see cref="IsolationLevel" /> and transaction timeout to be changed for the
        /// <see cref="TransactionScope" /> used to receive messages.
        /// </summary>
        /// <remarks>
        /// If not specified the default transaction timeout of the machine will be used and the isolation level will be set to
        /// <see cref="IsolationLevel.ReadCommitted" />.
        /// </remarks>
        [PreObsolete(Message = "TransactionScopeOptions has been obsoleted.",
            TreatAsErrorFromVersion = "8.0",
            RemoveInVersion = "9.0",
            ReplacementTypeOrMember = "SqlServerTransport.TransactionScope")]
        public static TransportExtensions<SqlServerTransport> TransactionScopeOptions(
            this TransportExtensions<SqlServerTransport> transport,
            TimeSpan? timeout = null,
            IsolationLevel? isolationLevel = null)
        {
            if (timeout.HasValue)
            {
                transport.Transport.TransactionScope.Timeout = timeout.Value;
            }

            if (isolationLevel.HasValue)
            {
                transport.Transport.TransactionScope.IsolationLevel = isolationLevel.Value;
            }

            return transport;
        }

        /// <summary>
        ///     Allows changing the queue peek delay, and the peek batch size.
        /// </summary>
        /// <param name="delay">The delay value</param>
        /// <param name="peekBatchSize">The peek batch size</param>
        /// <param name="transport">The transport settings to configure.</param>
        [PreObsolete(Message = "QueuePeekerOptions has been obsoleted.",
            TreatAsErrorFromVersion = "8.0",
            RemoveInVersion = "9.0",
            ReplacementTypeOrMember = "SqlServerTransport.QueuePeeker")]
        public static TransportExtensions<SqlServerTransport> QueuePeekerOptions(
            this TransportExtensions<SqlServerTransport> transport,
            TimeSpan? delay = null,
            int? peekBatchSize = null)
        {
            if (delay.HasValue)
            {
                transport.Transport.QueuePeeker.Delay = delay.Value;
            }

            transport.Transport.QueuePeeker.MaxRecordsToPeek = peekBatchSize;

            return transport;
        }

        /// <summary>
        /// Configures native delayed delivery.
        /// </summary>
        [PreObsolete(Message = "NativeDelayedDelivery has been obsoleted.",
            TreatAsErrorFromVersion = "8.0",
            RemoveInVersion = "9.0",
            ReplacementTypeOrMember = "SqlServerTransport.DelayedDelivery")]
        public static DelayedDeliverySettings NativeDelayedDelivery(
            this TransportExtensions<SqlServerTransport> transport) =>
            new DelayedDeliverySettings(transport.Transport.DelayedDelivery);

        /// <summary>
        /// Configures publish/subscribe behavior.
        /// </summary>
        [PreObsolete(Message = "SubscriptionSettings has been obsoleted.",
            TreatAsErrorFromVersion = "8.0",
            RemoveInVersion = "9.0",
            ReplacementTypeOrMember = "SqlServerTransport.Subscriptions")]
        public static SubscriptionSettings
            SubscriptionSettings(this TransportExtensions<SqlServerTransport> transport) =>
            new SubscriptionSettings(transport.Transport.Subscriptions);

        /// <summary>
        /// Instructs the transport to purge all expired messages from the input queue before starting the processing.
        /// </summary>
        /// <param name="purgeBatchSize">Size of the purge batch.</param>
        /// <param name="transport">The transport settings to configure.</param>
        [PreObsolete(Message = "PurgeExpiredMessagesOnStartup has been obsoleted.",
            TreatAsErrorFromVersion = "8.0",
            RemoveInVersion = "9.0",
            ReplacementTypeOrMember = "SqlServerTransport.PurgeOnStartup")]
        public static TransportExtensions<SqlServerTransport> PurgeExpiredMessagesOnStartup(
            this TransportExtensions<SqlServerTransport> transport,
            int? purgeBatchSize)
        {
            transport.Transport.ExpiredMessagesPurger.PurgeOnStartup = true;
            transport.Transport.ExpiredMessagesPurger.PurgeBatchSize = purgeBatchSize;

            return transport;
        }

        /// <summary>
        /// Instructs the transport to create a computed column for inspecting message body contents.
        /// </summary>
        [PreObsolete(Message = "CreateMessageBodyComputedColumn has been obsoleted.",
            TreatAsErrorFromVersion = "8.0",
            RemoveInVersion = "9.0",
            ReplacementTypeOrMember = "SqlServerTransport.CreateMessageBodyComputedColumn")]
        public static TransportExtensions<SqlServerTransport> CreateMessageBodyComputedColumn(
            this TransportExtensions<SqlServerTransport> transport)
        {
            transport.Transport.CreateMessageBodyComputedColumn = true;
            return transport;
        }
    }
}