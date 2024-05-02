#pragma warning disable PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext

namespace NServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Npgsql;
    using Transport.PostgreSql;

    /// <summary>
    /// Provides support for <see cref="UseTransport{T}"/> transport APIs.
    /// </summary>
    public static class PostgreSqlTransportSettingsExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<PostgreSqlTransport> UseTransport<T>(this EndpointConfiguration config)
            where T : PostgreSqlTransport
        {
            var transport = new PostgreSqlTransport();

            var routing = config.UseTransport(transport);

            var settings = new TransportExtensions<PostgreSqlTransport>(transport, routing);

            return settings;
        }

        /// <summary>
        ///     Sets a default schema for both input and output queues
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.DefaultSchema",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<PostgreSqlTransport> DefaultSchema(
            this TransportExtensions<PostgreSqlTransport> transportExtensions, string schemaName)
        {
            transportExtensions.Transport.DefaultSchema = schemaName;

            return transportExtensions;
        }

        /// <summary>
        ///     Specifies custom schema for given endpoint.
        /// </summary>
        /// <param name="endpointName">Endpoint name.</param>
        /// <param name="schema">Custom schema value.</param>
        /// <param name="transportExtensions">The transport settings to configure.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "RoutingSettings.UseSchemaForEndpoint",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<PostgreSqlTransport> UseSchemaForEndpoint(
            this TransportExtensions<PostgreSqlTransport> transportExtensions, string endpointName, string schema)
        {
            transportExtensions.Routing().UseSchemaForEndpoint(endpointName, schema);

            return transportExtensions;
        }

        /// <summary>
        /// Overrides schema value for given queue. This setting will take precedence over any other source of schema
        /// information.
        /// </summary>
        /// <param name="queueName">Queue name.</param>
        /// <param name="schema">Custom schema value.</param>
        /// <param name="transportExtensions">The transport settings to configure.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.SchemaAndCatalog.UseSchemaForQueue",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<PostgreSqlTransport> UseSchemaForQueue(
            this TransportExtensions<PostgreSqlTransport> transportExtensions, string queueName, string schema)
        {
            transportExtensions.Transport.Schema.UseSchemaForQueue(queueName, schema);

            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown
        /// procedure in case there are numerous errors while trying to receive messages.
        /// </summary>
        /// <param name="waitTime">Time to wait before triggering the circuit breaker.</param>
        /// <param name="transportExtensions">The transport settings to configure.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.TimeToWaitBeforeTriggeringCircuitBreaker",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<PostgreSqlTransport> TimeToWaitBeforeTriggeringCircuitBreaker(
            this TransportExtensions<PostgreSqlTransport> transportExtensions, TimeSpan waitTime)
        {
            transportExtensions.Transport.TimeToWaitBeforeTriggeringCircuitBreaker = waitTime;

            return transportExtensions;
        }

        /// <summary>
        /// Specifies connection factory to be used by sql transport.
        /// </summary>
        /// <param name="connectionString">Sql Server instance connection string.</param>
        /// <param name="transportExtensions">The transport settings to configure.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "configuration.UseTransport(new SqlServerTransport(string connectionString))",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<PostgreSqlTransport> ConnectionString(
            this TransportExtensions<PostgreSqlTransport> transportExtensions, string connectionString)
        {
            transportExtensions.Transport.ConnectionString = connectionString;

            return transportExtensions;
        }

        /// <summary>
        /// Specifies connection factory to be used by sql transport.
        /// </summary>
        /// <param name="sqlConnectionFactory">Factory that returns connection ready for usage.</param>
        /// <param name="transportExtensions">The transport settings to configure.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.ConnectionFactory",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<PostgreSqlTransport> UseCustomSqlConnectionFactory(
            this TransportExtensions<PostgreSqlTransport> transportExtensions,
            Func<CancellationToken, Task<NpgsqlConnection>> sqlConnectionFactory)
        {
            transportExtensions.Transport.ConnectionFactory = async ct => await sqlConnectionFactory(ct).ConfigureAwait(false);

            return transportExtensions;
        }

        /// <summary>
        /// Allows the <see cref="IsolationLevel" /> and transaction timeout to be changed for the
        /// <see cref="TransactionScope" /> used to receive messages.
        /// </summary>
        /// <remarks>
        /// If not specified the default transaction timeout of the machine will be used and the isolation level will be set to
        /// <see cref="IsolationLevel.ReadCommitted" />.
        /// </remarks>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.TransactionScope",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<PostgreSqlTransport> TransactionScopeOptions(
            this TransportExtensions<PostgreSqlTransport> transportExtensions,
            TimeSpan? timeout = null,
            IsolationLevel? isolationLevel = null)
        {
            if (timeout.HasValue)
            {
                transportExtensions.Transport.TransactionScope.Timeout = timeout.Value;
            }

            if (isolationLevel.HasValue)
            {
                transportExtensions.Transport.TransactionScope.IsolationLevel = isolationLevel.Value;
            }

            return transportExtensions;
        }

        /// <summary>
        ///     Allows changing the queue peek delay, and the peek batch size.
        /// </summary>
        /// <param name="delay">The delay value</param>
        /// <param name="peekBatchSize">The peek batch size</param>
        /// <param name="transportExtensions">The transport settings to configure.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.QueuePeeker",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<PostgreSqlTransport> QueuePeekerOptions(
            this TransportExtensions<PostgreSqlTransport> transportExtensions,
            TimeSpan? delay = null,
            int? peekBatchSize = null)
        {
            if (delay.HasValue)
            {
                transportExtensions.Transport.QueuePeeker.Delay = delay.Value;
            }

            transportExtensions.Transport.QueuePeeker.MaxRecordsToPeek = peekBatchSize;

            return transportExtensions;
        }

        /// <summary>
        /// Configures native delayed delivery.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.DelayedDelivery",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static DelayedDeliverySettings NativeDelayedDelivery(
            this TransportExtensions<PostgreSqlTransport> transportExtensions) =>
            new DelayedDeliverySettings(transportExtensions.Transport.DelayedDelivery);

        /// <summary>
        /// Configures publish/subscribe behavior.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.Subscriptions",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static SubscriptionSettings
            SubscriptionSettings(this TransportExtensions<PostgreSqlTransport> transportExtensions) =>
            new SubscriptionSettings(transportExtensions.Transport.Subscriptions);

        /// <summary>
        /// Instructs the transport to create a computed column for inspecting message body contents.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.CreateMessageBodyComputedColumn",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<PostgreSqlTransport> CreateMessageBodyComputedColumn(
            this TransportExtensions<PostgreSqlTransport> transportExtensions)
        {
            transportExtensions.Transport.CreateMessageBodyComputedColumn = true;
            return transportExtensions;
        }
    }

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.DelayedDelivery",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
    public partial class DelayedDeliverySettings
    {
        DelayedDeliveryOptions options;

        internal DelayedDeliverySettings(DelayedDeliveryOptions options) => this.options = options;

        /// <summary>
        /// Sets the suffix for the table storing delayed messages.
        /// </summary>
        /// <param name="suffix"></param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.TableSuffix",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void TableSuffix(string suffix)
        {
            Guard.AgainstNullAndEmpty(nameof(suffix), suffix);

            options.TableSuffix = suffix;
        }

        /// <summary>
        /// Sets the size of the batch when moving matured timeouts to the input queue.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.BatchSize",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void BatchSize(int batchSize)
        {
            if (batchSize <= 0)
            {
                throw new ArgumentException("Batch size has to be a positive number", nameof(batchSize));
            }

            options.BatchSize = batchSize;
        }
    }

    /// <summary>
    /// Configures the native pub/sub behavior
    /// </summary>
    [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.Subscriptions",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
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
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SubscriptionOptions.SubscriptionTableName",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void SubscriptionTableName(string tableName, string schemaName = null) => options.SubscriptionTableName = new SubscriptionTableName(tableName, schemaName);

        /// <summary>
        /// Cache subscriptions for a given <see cref="TimeSpan"/>.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void CacheSubscriptionInformationFor(TimeSpan timeSpan)
        {
            Guard.AgainstNegativeAndZero(nameof(timeSpan), timeSpan);
            options.CacheInvalidationPeriod = timeSpan;
        }

        /// <summary>
        /// Do not cache subscriptions.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SubscriptionOptions.DisableSubscriptionCache",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void DisableSubscriptionCache() => options.DisableCaching = true;
    }
}