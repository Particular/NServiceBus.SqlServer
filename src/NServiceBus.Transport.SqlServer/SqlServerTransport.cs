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
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// SqlServer Transport
    /// </summary>
    public partial class SqlServerTransport : TransportDefinition
    {
        QueueAddressTranslator addressTranslator;

        internal string Catalog { get; private set; }

        internal bool IsEncrypted { get; private set; }

        /// <summary>
        /// Creates and instance of <see cref="SqlServerTransport"/>
        /// </summary>
        public SqlServerTransport(string connectionString)
            : base(TransportTransactionMode.TransactionScope, true, true, true)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            ConnectionString = connectionString;
        }

        /// <summary>
        /// Creates and instance of <see cref="SqlServerTransport"/>
        /// </summary>
        public SqlServerTransport(SqlConnection sqlConnection)
            : base(TransportTransactionMode.TransactionScope, true, true, true)
        {
            SqlConnection = sqlConnection;
        }

        /// <summary>
        /// For the pub-sub migration tests only
        /// </summary>
        internal SqlServerTransport(string connectionString, bool supportsDelayedDelivery = true, bool supportsPublishSubscribe = true)
            : base(TransportTransactionMode.TransactionScope, supportsDelayedDelivery, supportsPublishSubscribe, true)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// <see cref="TransportDefinition.Initialize"/>
        /// </summary>
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            ValidateConfiguration();

            ParseConnectionAttributes();

            var infrastructure = new SqlServerTransportInfrastructure(this, hostSettings, addressTranslator, IsEncrypted);

            await infrastructure.ConfigureSubscriptions(Catalog, cancellationToken).ConfigureAwait(false);

            if (receivers.Length > 0)
            {
                await infrastructure.ConfigureReceiveInfrastructure(receivers, sendingAddresses, cancellationToken).ConfigureAwait(false);
            }

            infrastructure.ConfigureSendInfrastructure();

            return infrastructure;
        }

        internal void ParseConnectionAttributes()
        {
            if (addressTranslator != null)
            {
                return;
            }

            var parser = GetConnectionStringBuilder();

            if (DefaultCatalog != null)
            {
                Catalog = DefaultCatalog;
            }
            else
            {
                if (!parser.TryGetValue("Initial Catalog", out var catalogSetting) && !parser.TryGetValue("database", out catalogSetting))
                {
                    throw new Exception("Initial Catalog property is mandatory in the connection string.");
                }
                Catalog = (string)catalogSetting;
            }

            if (parser.TryGetValue("Column Encryption Setting", out var enabled))
            {
                IsEncrypted = ((string)enabled).Equals("enabled", StringComparison.InvariantCultureIgnoreCase);
            }

            addressTranslator = new QueueAddressTranslator(Catalog, "dbo", DefaultSchema, SchemaAndCatalog);
        }

        DbConnectionStringBuilder GetConnectionStringBuilder()
        {
            if (SqlConnection != null)
            {
                return new DbConnectionStringBuilder { ConnectionString = SqlConnection.ConnectionString };
            }

            return new DbConnectionStringBuilder { ConnectionString = ConnectionString };
        }

        /// <summary>
        /// Translates a <see cref="QueueAddress"/> object into a transport specific queue address-string.
        /// </summary>
        public override string ToTransportAddress(Transport.QueueAddress address)
        {
            ParseConnectionAttributes();

            if (addressTranslator == null)
            {
                throw new Exception("Initialize must be called before using ToTransportAddress");
            }

            var tableQueueAddress = addressTranslator.Generate(address);

            return addressTranslator.GetCanonicalForm(tableQueueAddress).Address;
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
        public string ConnectionString { get; internal set; }

        /// <summary>
        /// Connection string factory.
        /// </summary>
        public SqlConnection SqlConnection { get; internal set; }

        /// <summary>
        /// Callback.
        /// </summary>
        public Action<SqlConnection> OnSqlConnectionConnected { get; set; }

        /// <summary>
        /// Default address schema.
        /// </summary>
        public string DefaultSchema { get; set; } = string.Empty;

        /// <summary>
        /// Default address catalog.
        /// </summary>
        public string DefaultCatalog { get; set; }

        /// <summary>
        /// Catalog and schema configuration for SQL Transport queues.
        /// </summary>
        public QueueSchemaAndCatalogOptions SchemaAndCatalog { get; } = new QueueSchemaAndCatalogOptions();

        /// <summary>
        /// Subscription infrastructure settings.
        /// </summary>
        public SubscriptionOptions Subscriptions { get; } = new SubscriptionOptions();

        /// <summary>
        /// Transaction scope settings.
        /// </summary>
        public TransactionScopeOptions TransactionScope { get; } = new TransactionScopeOptions();

        /// <summary>
        /// Time to wait before triggering the circuit breaker.
        /// </summary>
        public TimeSpan TimeToWaitBeforeTriggeringCircuitBreaker { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Queue peeker settings.
        /// </summary>
        public QueuePeekerOptions QueuePeeker { get; set; } = new QueuePeekerOptions();

        /// <summary>
        /// Instructs the transport to create a computed column for inspecting message body contents.
        /// </summary>
        public bool CreateMessageBodyComputedColumn { get; set; } = false;

        /// <summary>
        /// Expired messages purger settings.
        /// </summary>
        public ExpiredMessagesPurgerOptions ExpiredMessagesPurger { get; } = new ExpiredMessagesPurgerOptions();

        /// <summary>
        /// Delayed delivery infrastructure configuration
        /// </summary>
        public DelayedDeliveryOptions DelayedDelivery { get; } = new DelayedDeliveryOptions();

        /// <summary>
        /// Disable native delayed delivery infrastructure
        /// </summary>
        ///TODO: this is for SC usage only. It should not be public
        internal bool DisableDelayedDelivery { get; set; } = false;

        internal TestingInformation Testing { get; } = new TestingInformation();

        internal class TestingInformation
        {
            internal Func<string, TableBasedQueue> QueueFactoryOverride { get; set; } = null;

            internal string[] ReceiveAddresses { get; set; }

            internal string[] SendingAddresses { get; set; }

            internal string DelayedDeliveryQueue { get; set; }

            internal string SubscriptionTable { get; set; }
        }
    }
}