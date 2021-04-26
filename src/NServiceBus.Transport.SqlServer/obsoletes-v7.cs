#pragma warning disable PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext

namespace NServiceBus.Transport.SqlServer
{
    using System;

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    [ObsoleteEx(Message = "DelayedDeliverySettings has been obsoleted.",
        ReplacementTypeOrMember = "SqlServerTransport.DelayedDelivery", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
    public class DelayedDeliverySettings
    {
        DelayedDeliveryOptions options;

        internal DelayedDeliverySettings(DelayedDeliveryOptions options) => this.options = options;

        /// <summary>
        /// Enables the timeout manager for the endpoint.
        /// </summary>
        [ObsoleteEx(
            Message = "Timeout manager has been removed from NServiceBus. See the upgrade guide for more details.",
            RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
        public void EnableTimeoutManagerCompatibility() => throw new InvalidOperationException();

        /// <summary>
        /// Sets the suffix for the table storing delayed messages.
        /// </summary>
        /// <param name="suffix"></param>
        [ObsoleteEx(ReplacementTypeOrMember = "SqlServerTransport.TableSuffix", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
        public void TableSuffix(string suffix)
        {
            Guard.AgainstNullAndEmpty(nameof(suffix), suffix);

            options.TableSuffix = suffix;
        }

        /// <summary>
        /// Sets the size of the batch when moving matured timeouts to the input queue.
        /// </summary>
        [ObsoleteEx(ReplacementTypeOrMember = "SqlServerTransport.BatchSize", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
        public void BatchSize(int batchSize)
        {
            if (batchSize <= 0)
            {
                throw new ArgumentException("Batch size has to be a positive number", nameof(batchSize));
            }

            options.BatchSize = batchSize;
        }

        /// <summary>
        /// Configures how often delayed messages are processed (every 5 seconds by default).
        /// </summary>
        [ObsoleteEx(ReplacementTypeOrMember = "SqlServerTransport.ProcessingInterval", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
        public void ProcessingInterval(TimeSpan interval)
        {
            Guard.AgainstNegativeAndZero(nameof(interval), interval);

            options.ProcessingInterval = interval;
        }
    }

    /// <summary>
    /// Configures the native pub/sub behavior
    /// </summary>
    [ObsoleteEx(Message = "SubscriptionSettings has been obsoleted.",
        ReplacementTypeOrMember = "SqlServerTransport.Subscriptions", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
    public class SubscriptionSettings
    {
        SubscriptionOptions options;

        internal SubscriptionSettings(SubscriptionOptions options) => this.options = options;

        /// <summary>
        /// Overrides the default name for the subscription table. All endpoints in a given system need to agree on that name in order for them to be able
        /// to subscribe to and publish events.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="schemaName">Schema in which the table is defined if different from default schema configured for the transport.</param>
        /// <param name="catalogName">Catalog in which the table is defined if different from default catalog configured for the transport.</param>
        [ObsoleteEx(ReplacementTypeOrMember = "SubscriptionOptions.SubscriptionTableName", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
        public void SubscriptionTableName(string tableName, string schemaName = null, string catalogName = null) => options.SubscriptionTableName = new SubscriptionTableName(tableName, schemaName, catalogName);

        /// <summary>
        /// Cache subscriptions for a given <see cref="TimeSpan"/>.
        /// </summary>
        [ObsoleteEx(ReplacementTypeOrMember = "SubscriptionOptions.CacheSubscriptionInformationFor", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
        public void CacheSubscriptionInformationFor(TimeSpan timeSpan)
        {
            Guard.AgainstNegativeAndZero(nameof(timeSpan), timeSpan);
            options.CacheInvalidationPeriod = timeSpan;
        }

        /// <summary>
        /// Do not cache subscriptions.
        /// </summary>
        [ObsoleteEx(ReplacementTypeOrMember = "SubscriptionOptions.DisableSubscriptionCache", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
        public void DisableSubscriptionCache() => options.DisableCaching = true;
    }
}

namespace NServiceBus
{
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System;
    using System.Threading.Tasks;
    using System.Transactions;
    using Transport.SqlServer;

    /// <summary>
    /// SqlServer Transport
    /// </summary>
    public partial class SqlServerTransport
    {
        /// <summary>
        /// Used for backwards compatibility with the legacy transport api.
        /// </summary>
        internal SqlServerTransport()
            : base(TransportTransactionMode.TransactionScope, true, true, true)
        {

        }

        void ValidateConfiguration()
        {
            //This is needed due to legacy transport api support. It can be removed when the api is no longer supported.
            if (ConnectionFactory == null && string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new Exception("SqlServer transport requires connection string or connection factory.");
            }

            if (ConnectionFactory != null && !string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new Exception("ConnectionString() and UseCustomConnectionFactory() settings are exclusive and can't be used at the same time.");
            }

        }
    }

    /// <summary>
    /// Provides support for <see cref="UseTransport{T}"/> transport APIs.
    /// </summary>
    public static class SqlServerTransportApiExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>
        /// <param name="config">Endpoint configuration instance.</param>
        [ObsoleteEx(
            RemoveInVersion = "9",
            TreatAsErrorFromVersion = "8",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(new SqlServerTransport())")]
        public static SqlServerTransportSettings UseTransport<T>(this EndpointConfiguration config)
            where T : SqlServerTransport
        {
            var transport = new SqlServerTransport();

            var routing = config.UseTransport(transport);

            var settings = new SqlServerTransportSettings(transport, routing);

            return settings;
        }

        /// <summary>
        /// Learning transport configuration settings.
        /// </summary>
        public class SqlServerTransportSettings : TransportSettings<SqlServerTransport>
        {
            internal SqlServerTransport SqlTransport;

            internal SqlServerTransportSettings(SqlServerTransport transport, RoutingSettings<SqlServerTransport> routing)
                : base(transport, routing) => SqlTransport = transport;

            /// <summary>
            ///     Sets a default schema for both input and output queues
            /// </summary>
            [ObsoleteEx(Message = "DefaultSchema has been obsoleted.",
                ReplacementTypeOrMember = "SqlServerTransport.DefaultSchema", RemoveInVersion = "9", TreatAsErrorFromVersion = "8")]
            public SqlServerTransportSettings DefaultSchema(string schemaName)
            {
                Transport.DefaultSchema = schemaName;

                return this;
            }

            /// <summary>
            ///     Specifies custom schema for given endpoint.
            /// </summary>
            /// <param name="endpointName">Endpoint name.</param>
            /// <param name="schema">Custom schema value.</param>
            [ObsoleteEx(Message = "UseSchemaForEndpoint has been obsoleted.",
                ReplacementTypeOrMember = "RoutingSettings.UseSchemaForEndpoint", RemoveInVersion = "9", TreatAsErrorFromVersion = "7")]
            public SqlServerTransportSettings UseSchemaForEndpoint(string endpointName, string schema) =>
                throw new NotImplementedException();

            /// <summary>
            /// Overrides schema value for given queue. This setting will take precedence over any other source of schema
            /// information.
            /// </summary>
            /// <param name="queueName">Queue name.</param>
            /// <param name="schema">Custom schema value.</param>
            [ObsoleteEx(Message = "UseSchemaForQueue has been obsoleted.",
                ReplacementTypeOrMember = "SqlServerTransport.SchemaAndCatalog.UseSchemaForQueue", RemoveInVersion = "9", TreatAsErrorFromVersion = "8")]
            public SqlServerTransportSettings UseSchemaForQueue(string queueName, string schema)
            {
                Transport.SchemaAndCatalog.UseSchemaForQueue(queueName, schema);

                return this;
            }

            /// <summary>
            ///  Specifies custom schema for given endpoint.
            /// </summary>
            /// <param name="endpointName">Endpoint name.</param>
            /// <param name="catalog">Custom catalog value.</param>
            [ObsoleteEx(Message = "UseCatalogForEndpoint has been obsoleted.",
                ReplacementTypeOrMember = "RoutingSettings.UseCatalogForEndpoint", RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
            public SqlServerTransportSettings UseCatalogForEndpoint(string endpointName, string catalog) =>
                throw new NotImplementedException();

            /// <summary>
            /// Specifies custom schema for given queue.
            /// </summary>
            /// <param name="queueName">Queue name.</param>
            /// <param name="catalog">Custom catalog value.</param>
            [ObsoleteEx(Message = "UseCatalogForQueue has been obsoleted.",
                ReplacementTypeOrMember = "SqlServerTransport.SchemaAndCatalog.UseCatalogForQueue", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
            public SqlServerTransportSettings UseCatalogForQueue(string queueName, string catalog)
            {
                Transport.SchemaAndCatalog.UseCatalogForQueue(queueName, catalog);

                return this;
            }

            /// <summary>
            /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown
            /// procedure in case there are numerous errors while trying to receive messages.
            /// </summary>
            /// <param name="waitTime">Time to wait before triggering the circuit breaker.</param>
            [ObsoleteEx(Message = "TimeToWaitBeforeTriggeringCircuitBreaker has been obsoleted.",
                ReplacementTypeOrMember = "SqlServerTransport.TimeToWaitBeforeTriggeringCircuitBreaker", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
            public SqlServerTransportSettings TimeToWaitBeforeTriggeringCircuitBreaker(TimeSpan waitTime)
            {
                Transport.TimeToWaitBeforeTriggeringCircuitBreaker = waitTime;

                return this;
            }

            /// <summary>
            /// Specifies connection factory to be used by sql transport.
            /// </summary>
            /// <param name="connectionString">Sql Server instance connection string.</param>
            [ObsoleteEx(Message = "ConnectionString has been obsoleted.",
                ReplacementTypeOrMember = "configuration.UseTransport(new SqlServerTransport(string connectionString))", RemoveInVersion = "9.0",
                TreatAsErrorFromVersion = "8.0")]
            public SqlServerTransportSettings ConnectionString(string connectionString)
            {
                Transport.ConnectionString = connectionString;

                return this;
            }

            /// <summary>
            /// Specifies connection factory to be used by sql transport.
            /// </summary>
            /// <param name="sqlConnectionFactory">Factory that returns connection ready for usage.</param>
            [ObsoleteEx(Message = "UseCustomSqlConnectionFactory has been obsoleted.",
                ReplacementTypeOrMember = "SqlServerTransport.ConnectionFactory", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
            public SqlServerTransportSettings UseCustomSqlConnectionFactory(
                Func<Task<SqlConnection>> sqlConnectionFactory)
            {
                Transport.ConnectionFactory = async (_) => await sqlConnectionFactory().ConfigureAwait(false);

                return this;
            }

            /// <summary>
            /// Allows the <see cref="IsolationLevel" /> and transaction timeout to be changed for the
            /// <see cref="TransactionScope" /> used to receive messages.
            /// </summary>
            /// <remarks>
            /// If not specified the default transaction timeout of the machine will be used and the isolation level will be set to
            /// <see cref="IsolationLevel.ReadCommitted" />.
            /// </remarks>
            [ObsoleteEx(Message = "TransactionScopeOptions has been obsoleted.",
                ReplacementTypeOrMember = "SqlServerTransport.TransactionScope", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
            public SqlServerTransportSettings TransactionScopeOptions(TimeSpan? timeout = null,
                IsolationLevel? isolationLevel = null)
            {
                if (timeout.HasValue)
                {
                    Transport.TransactionScope.Timeout = timeout.Value;
                }

                if (isolationLevel.HasValue)
                {
                    Transport.TransactionScope.IsolationLevel = isolationLevel.Value;
                }

                return this;
            }

            /// <summary>
            ///     Allows changing the queue peek delay, and the peek batch size.
            /// </summary>
            /// <param name="delay">The delay value</param>
            /// <param name="peekBatchSize">The peek batch size</param>
            [ObsoleteEx(Message = "QueuePeekerOptions has been obsoleted.",
                ReplacementTypeOrMember = "SqlServerTransport.QueuePeeker", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
            public SqlServerTransportSettings QueuePeekerOptions(TimeSpan? delay = null, int? peekBatchSize = null)
            {
                if (delay.HasValue)
                {
                    Transport.QueuePeeker.Delay = delay.Value;
                }

                Transport.QueuePeeker.MaxRecordsToPeek = peekBatchSize;

                return this;
            }

            /// <summary>
            /// Configures native delayed delivery.
            /// </summary>
            [ObsoleteEx(Message = "NativeDelayedDelivery has been obsoleted.",
                ReplacementTypeOrMember = "SqlServerTransport.DelayedDelivery", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
            public DelayedDeliverySettings NativeDelayedDelivery() => new DelayedDeliverySettings(Transport.DelayedDelivery);

            /// <summary>
            /// Configures publish/subscribe behavior.
            /// </summary>
            [ObsoleteEx(Message = "SubscriptionSettings has been obsoleted.",
                ReplacementTypeOrMember = "SqlServerTransport.Subscriptions", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
            public SubscriptionSettings SubscriptionSettings() => new SubscriptionSettings(Transport.Subscriptions);

            /// <summary>
            /// Instructs the transport to purge all expired messages from the input queue before starting the processing.
            /// </summary>
            /// <param name="purgeBatchSize">Size of the purge batch.</param>
            [ObsoleteEx(Message = "PurgeExpiredMessagesOnStartup has been obsoleted.",
                ReplacementTypeOrMember = "SqlServerTransport.PurgeOnStartup", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
            public SqlServerTransportSettings PurgeExpiredMessagesOnStartup(int? purgeBatchSize)
            {
                Transport.ExpiredMessagesPurger.PurgeOnStartup = true;
                Transport.ExpiredMessagesPurger.PurgeBatchSize = purgeBatchSize;
                return this;
            }

            /// <summary>
            /// Instructs the transport to create a computed column for inspecting message body contents.
            /// </summary>
            [ObsoleteEx(Message = "CreateMessageBodyComputedColumn has been obsoleted.",
                ReplacementTypeOrMember = "SqlServerTransport.CreateMessageBodyComputedColumn", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
            public SqlServerTransportSettings CreateMessageBodyComputedColumn()
            {
                Transport.CreateMessageBodyComputedColumn = true;
                return this;
            }
        }
    }

    /// <summary>
    /// Configuration extensions for Message-Driven Pub-Sub compatibility mode
    /// </summary>
    public static partial class MessageDrivenPubSubCompatibilityModeConfiguration
    {
        /// <summary>
        ///    Enables compatibility with endpoints running on message-driven pub-sub
        /// </summary>
        /// <param name="transportExtensions">The transport to enable pub-sub compatibility on</param>
        [ObsoleteEx(Message = "EnableMessageDrivenPubSubCompatibilityMode has been obsoleted.",
            ReplacementTypeOrMember = "RoutingSettings.EnableMessageDrivenPubSubCompatibilityMode",
            RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
#pragma warning disable 618
        public static SubscriptionMigrationModeSettings EnableMessageDrivenPubSubCompatibilityMode(
            this TransportExtensions<SqlServerTransport> transportExtensions)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }
    }
}