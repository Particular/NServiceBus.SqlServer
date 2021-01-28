using System;
using System.Threading.Tasks;
using System.Transactions;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Logging;
using NServiceBus.Transport.SqlServer;
#if SYSTEMDATASQLCLIENT
using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif


namespace NServiceBus
{
    /// <summary>
    ///     Configuration extensions for Message-Driven Pub-Sub compatibility mode
    /// </summary>
    public static partial class MessageDrivenPubSubCompatibilityModeConfiguration
    {
        /// <summary>
        ///     Enables compatibility with endpoints running on message-driven pub-sub
        /// </summary>
        /// <param name="transportExtensions">The transport to enable pub-sub compatibility on</param>
        [ObsoleteEx(Message = "EnableMessageDrivenPubSubCompatibilityMode has been obsoleted.",
            ReplacementTypeOrMember = "RoutingSettings", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static SubscriptionMigrationModeSettings EnableMessageDrivenPubSubCompatibilityMode(
            this TransportExtensions<SqlServerTransport> transportExtensions)
#pragma warning restore 618
        {
            var settings = transportExtensions.GetSettings();
            settings.Set("NServiceBus.Subscriptions.EnableMigrationMode", true);
            return new SubscriptionMigrationModeSettings(settings);
        }
    }

    /// <summary>
    ///     Adds extra configuration for the Sql Server transport.
    /// </summary>
    public static class SqlServerTransportSettingsExtensions
    {
        static ILog Logger = LogManager.GetLogger<SqlServerTransport>();

        /// <summary>
        ///     Sets a default schema for both input and output queues
        /// </summary>
        [ObsoleteEx(Message = "DefaultSchema has been obsoleted.",
            ReplacementTypeOrMember = "SqlServerTransport.DefaultSchema", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static TransportExtensions<SqlServerTransport> DefaultSchema(
            this TransportExtensions<SqlServerTransport> transportExtensions, string schemaName)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Specifies custom schema for given endpoint.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="endpointName">Endpoint name.</param>
        /// <param name="schema">Custom schema value.</param>
        [ObsoleteEx(Message = "UseSchemaForEndpoint has been obsoleted.",
            ReplacementTypeOrMember = "RoutingSettings.UseSchemaForEndpoint", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static TransportExtensions<SqlServerTransport> UseSchemaForEndpoint(
            this TransportExtensions<SqlServerTransport> transportExtensions, string endpointName, string schema)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Overrides schema value for given queue. This setting will take precedence over any other source of schema
        ///     information.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="queueName">Queue name.</param>
        /// <param name="schema">Custom schema value.</param>
        [ObsoleteEx(Message = "UseSchemaForQueue has been obsoleted.",
            ReplacementTypeOrMember = "SqlServerTransport.SchemaAndCatalog.UseSchemaForQueue", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static TransportExtensions<SqlServerTransport> UseSchemaForQueue(
            this TransportExtensions<SqlServerTransport> transportExtensions, string queueName, string schema)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Specifies custom schema for given endpoint.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="endpointName">Endpoint name.</param>
        /// <param name="catalog">Custom catalog value.</param>
        [ObsoleteEx(Message = "UseCatalogForEndpoint has been obsoleted.",
            ReplacementTypeOrMember = "RoutingSettings.UseCatalogForEndpoint", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static TransportExtensions<SqlServerTransport> UseCatalogForEndpoint(
            this TransportExtensions<SqlServerTransport> transportExtensions, string endpointName, string catalog)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Specifies custom schema for given queue.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="queueName">Queue name.</param>
        /// <param name="catalog">Custom catalog value.</param>
        [ObsoleteEx(Message = "UseCatalogForQueue has been obsoleted.",
            ReplacementTypeOrMember = "SqlServerTransport.SchemaAndCatalog.UseCatalogForQueue", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static TransportExtensions<SqlServerTransport> UseCatalogForQueue(
            this TransportExtensions<SqlServerTransport> transportExtensions, string queueName, string catalog)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown
        ///     procedure
        ///     in case there are numerous errors
        ///     while trying to receive messages.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="waitTime">Time to wait before triggering the circuit breaker.</param>
        [ObsoleteEx(Message = "TimeToWaitBeforeTriggeringCircuitBreaker has been obsoleted.",
            ReplacementTypeOrMember = "SqlServerTransport.TimeToWaitBeforeTriggering", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static TransportExtensions<SqlServerTransport> TimeToWaitBeforeTriggeringCircuitBreaker(
            this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan waitTime)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Specifies connection factory to be used by sql transport.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="sqlConnectionFactory">Factory that returns connection ready for usage.</param>
        [ObsoleteEx(Message = "UseCustomSqlConnectionFactory has been obsoleted.",
            ReplacementTypeOrMember = "SqlServerTransport.ConnectionFactory", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static TransportExtensions<SqlServerTransport> UseCustomSqlConnectionFactory(
            this TransportExtensions<SqlServerTransport> transportExtensions,
            Func<Task<SqlConnection>> sqlConnectionFactory)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Allows the <see cref="IsolationLevel" /> and transaction timeout to be changed for the
        ///     <see cref="TransactionScope" /> used to receive messages.
        /// </summary>
        /// <remarks>
        ///     If not specified the default transaction timeout of the machine will be used and the isolation level will be set to
        ///     <see cref="IsolationLevel.ReadCommitted" />.
        /// </remarks>
        [ObsoleteEx(Message = "TransactionScopeOptions has been obsoleted.",
            ReplacementTypeOrMember = "SqlServerTransport.TransactionScopeOptions", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static TransportExtensions<SqlServerTransport> TransactionScopeOptions(
            this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan? timeout = null,
            IsolationLevel? isolationLevel = null)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Allows changing the queue peek delay, and the peek batch size.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="delay">The delay value</param>
        /// <param name="peekBatchSize">The peek batch size</param>
        [ObsoleteEx(Message = "QueuePeekerOptions has been obsoleted.",
            ReplacementTypeOrMember = "SqlServerTransport.QueuePeeker", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static TransportExtensions<SqlServerTransport> QueuePeekerOptions(
            this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan? delay = null,
            int? peekBatchSize = null)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Configures native delayed delivery.
        /// </summary>
        [ObsoleteEx(Message = "NativeDelayedDelivery has been obsoleted.",
            ReplacementTypeOrMember = "SqlServerTransport.DelayedDelivery", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static DelayedDeliverySettings NativeDelayedDelivery(
            this TransportExtensions<SqlServerTransport> transportExtensions)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Configures publish/subscribe behavior.
        /// </summary>
        [ObsoleteEx(Message = "SubscriptionSettings has been obsoleted.",
            ReplacementTypeOrMember = "SqlServerTransport.Subscriptions", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static SubscriptionSettings SubscriptionSettings(
            this TransportExtensions<SqlServerTransport> transportExtensions)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Instructs the transport to purge all expired messages from the input queue before starting the processing.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="purgeBatchSize">Size of the purge batch.</param>
        [ObsoleteEx(Message = "PurgeExpiredMessagesOnStartup has been obsoleted.",
            ReplacementTypeOrMember = "SqlServerTransport.PurgeExpiredMessagesOnStartup", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static TransportExtensions<SqlServerTransport> PurgeExpiredMessagesOnStartup(
            this TransportExtensions<SqlServerTransport> transportExtensions, int? purgeBatchSize)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Instructs the transport to create a computed column for inspecting message body contents.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        [ObsoleteEx(Message = "CreateMessageBodyComputedColumn has been obsoleted.",
            ReplacementTypeOrMember = "SqlServerTransport.CreateMessageBodyComputedColumn", RemoveInVersion = "9.0",
            TreatAsErrorFromVersion = "8.0")]
#pragma warning disable 618
        public static TransportExtensions<SqlServerTransport> CreateMessageBodyComputedColumn(
            this TransportExtensions<SqlServerTransport> transportExtensions)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }
    }
}