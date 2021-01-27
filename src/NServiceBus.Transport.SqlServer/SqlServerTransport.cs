using System.Collections.Generic;

namespace NServiceBus
{
    using System;
    using System.Data.Common;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using Transport;
    using Transport.SqlServer;

    /// <summary>
    /// SqlServer Transport
    /// </summary>
    public class SqlServerTransport : TransportDefinition
    {
        string connectionString;
        Func<Task<SqlConnection>> connectionFactory;

        QueueAddressTranslator addressTranslator;
        string catalog;

        DbConnectionStringBuilder GetConnectionStringBuilder()
        {
            if (connectionFactory != null)
            {
                using (var connection = connectionFactory().GetAwaiter().GetResult())
                {
                    return new DbConnectionStringBuilder {ConnectionString = connection.ConnectionString};
                }
            }

            return new DbConnectionStringBuilder {ConnectionString = connectionString};
        }

        internal string GetDefaultCatalog()
        {
            var parser = GetConnectionStringBuilder();

            if (parser.TryGetValue("Initial Catalog", out var catalog) ||
                parser.TryGetValue("database", out catalog))
            {
                return (string)catalog;
            }

            throw new Exception("Initial Catalog property is mandatory in the connection string.");
        }

        bool IsEncrypted()
        {
            var parser = GetConnectionStringBuilder();

            if (parser.TryGetValue("Column Encryption Setting", out var enabled))
            {
                return ((string)enabled).Equals("enabled", StringComparison.InvariantCultureIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// Creates and instance of <see cref="SqlServerTransport"/>
        /// </summary>
        public SqlServerTransport() 
            : base(TransportTransactionMode.TransactionScope, true, true, true)
        {
        }

        /// <summary>
        /// For the pub-sub migration tests only
        /// </summary>
        internal SqlServerTransport(bool supportsDelayedDelivery = true, bool supportsPublishSubscribe = true)
            : base(TransportTransactionMode.TransactionScope, supportsDelayedDelivery, supportsPublishSubscribe, true)
        {
        }

        /// <summary>
        /// <see cref="TransportDefinition.Initialize"/>
        /// </summary>
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses)
        {
            FinalizeConfiguration();
            
            var infrastructure = new SqlServerTransportInfrastructure(this, hostSettings, addressTranslator, IsEncrypted());
            
            await infrastructure.ConfigureSubscriptions(hostSettings, catalog).ConfigureAwait(false);

            if (receivers.Length > 0)
            {
                await infrastructure.ConfigureReceiveInfrastructure(receivers, sendingAddresses).ConfigureAwait(false);
            }

            infrastructure.ConfigureSendInfrastructure();

            return infrastructure;
        }

        /// <summary>
        /// Translates a <see cref="QueueAddress"/> object into a transport specific queue address-string.
        /// </summary>
        public override string ToTransportAddress(Transport.QueueAddress address)
        {
            FinalizeConfiguration();

            var tableQueueAddress = addressTranslator.Generate(address);

            return addressTranslator.GetCanonicalForm(tableQueueAddress).Address;
        }

        void FinalizeConfiguration()
        {
            if (addressTranslator == null)
            {
                catalog = GetDefaultCatalog();

                addressTranslator = new QueueAddressTranslator(catalog, "dbo", DefaultSchema, QueueSchemaAndCatalogSettings);
            }
        }

        /// <summary>
        /// <see cref="TransportDefinition.GetSupportedTransactionModes"/>
        /// </summary>
        public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes()
        {
            return new[]
            {
                TransportTransactionMode.None,
                TransportTransactionMode.ReceiveOnly,
                TransportTransactionMode.SendsAtomicWithReceive,
                TransportTransactionMode.TransactionScope
            };
        }

        /// <summary>
        /// Connection string to be used by the transport.
        /// </summary>
        public string ConnectionString
        {
            get => connectionString;
            set
            {
                if (ConnectionFactory != null)
                {
                    throw new ArgumentException($"{nameof(ConnectionFactory)} has already been set. {nameof(ConnectionString)} and {nameof(ConnectionFactory)} cannot be used at the same time.");
                }

                connectionString = value;
            }
        }


        /// <summary>
        /// Connection string factory.
        /// </summary>
        public Func<Task<SqlConnection>> ConnectionFactory 
        {
            get => connectionFactory;
            set
            {
                if (string.IsNullOrWhiteSpace(ConnectionString) == false)
                {
                    throw new ArgumentException($"{nameof(ConnectionString)} has already been set. {nameof(ConnectionString)} and {nameof(ConnectionFactory)} cannot be used at the same time.");
                }

                connectionFactory = value;
            } 
        }

        /// <summary>
        /// Default address schema.
        /// </summary>
        public string DefaultSchema { get; set; } = string.Empty;

        /// <summary>
        /// Catalog and schema configuration for SQL Transport queues.
        /// </summary>
        public QueueSchemaAndCatalogSettings QueueSchemaAndCatalogSettings { get; } = new QueueSchemaAndCatalogSettings();

        /// <summary>
        /// Subscription infrastructure settings.
        /// </summary>
        public SubscriptionSettings Subscriptions { get; } = new SubscriptionSettings();

        /// <summary>
        /// Transaction scope options settings.
        /// </summary>
        public SqlScopeOptions ScopeOptions { get; } = new SqlScopeOptions();

        /// <summary>
        /// Time to wait before triggering the circuit breaker.
        /// </summary>
        public TimeSpan TimeToWaitBeforeTriggering { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Queue peeker settings.
        /// </summary>
        public QueuePeekerOptions QueuePeekerOptions { get; set; } = new QueuePeekerOptions();

        /// <summary>
        /// Instructs the transport to create a computed column for inspecting message body contents.
        /// </summary>
        public bool CreateMessageBodyComputedColumn { get; set; } = false;

        /// <summary>
        /// Instructs the transport to purge all expired messages from the input queue before starting the processing.
        /// </summary>
        public bool PurgeExpiredMessagesOnStartup { get; set; } = false;

        /// <summary>
        /// Maximum number of messages used in each delete batch when message purging on startup is enabled.
        /// </summary>
        public int? PurgeExpiredMessagesBatchSize { get; set; } = null;

        /// <summary>
        /// Delayed delivery infrastructure configuration
        /// </summary>
        public DelayedDeliverySettings DelayedDelivery { get; set; } = new DelayedDeliverySettings();

        /// <summary>
        /// Disable native delayed delivery infrastructure
        /// </summary>
        public bool DisableDelayedDelivery { get; set; } = false;

        internal TestingInformation Testing { get; } = new TestingInformation(); 

        internal class TestingInformation
        {
            internal Func<string, TableBasedQueue> QueueFactoryOverride { get; set; } = null;

            internal string[] ReceiveAddresses { get;  set; }

            internal string[] SendingAddresses { get; set; }

            internal string DelayedDeliveryQueue { get; set; }

            internal string SubscriptionTable { get; set; }
        }
    }
}