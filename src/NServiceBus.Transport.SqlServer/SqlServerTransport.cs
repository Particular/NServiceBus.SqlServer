namespace NServiceBus
{
    using System;
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
    using System.Transactions;

    /// <summary>
    /// SqlServer Transport
    /// </summary>
    public class SqlServerTransport : TransportDefinition
    {
        /// <summary>
        /// Creates and instance of <see cref="SqlServerTransport"/>
        /// </summary>
        public SqlServerTransport(string connectionString)
            : base(DefaultTransactionMode, true, true, true)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            ConnectionString = connectionString;
        }

        /// <summary>
        /// Creates and instance of <see cref="SqlServerTransport"/>
        /// </summary>
        /// <param name="connectionFactory">Connection factory that returns an instance of <see cref="SqlConnection"/> in an Opened state.</param>
        public SqlServerTransport(Func<CancellationToken, Task<SqlConnection>> connectionFactory)
            : base(DefaultTransactionMode, true, true, true)
        {
            Guard.AgainstNull(nameof(connectionFactory), connectionFactory);

            ConnectionFactory = connectionFactory;
        }

        /// <summary>
        /// Used for backwards compatibility with the legacy transport api.
        /// </summary>
        internal SqlServerTransport()
            : base(DefaultTransactionMode, true, true, true)
        {
        }

        /// <summary>
        /// For the pub-sub migration tests only
        /// </summary>
        internal SqlServerTransport(string connectionString, bool supportsDelayedDelivery = true, bool supportsPublishSubscribe = true)
            : base(DefaultTransactionMode, supportsDelayedDelivery, supportsPublishSubscribe, true)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// <see cref="TransportDefinition.Initialize"/>
        /// </summary>
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            ValidateConfiguration();

            if (TransportTransactionMode == TransportTransactionMode.TransactionScope)
            {
                TransactionManager.ImplicitDistributedTransactions = true;
            }

            var infrastructure = new SqlServerTransportInfrastructure(this, hostSettings, receivers, sendingAddresses);

            await infrastructure.Initialize(cancellationToken).ConfigureAwait(false);

            return infrastructure;
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
                throw new Exception(
                    "ConnectionString() and UseCustomConnectionFactory() settings are exclusive and can't be used at the same time.");
            }
        }

        /// <summary>
        /// <see cref="TransportDefinition.GetSupportedTransactionModes"/>
        /// </summary>
        public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes()
        {
            var supportedModes = new List<TransportTransactionMode>(){
                TransportTransactionMode.None,
                TransportTransactionMode.ReceiveOnly,
                TransportTransactionMode.SendsAtomicWithReceive
            };

            if (OperatingSystem.IsWindows())
            {
                supportedModes.Add(TransportTransactionMode.TransactionScope);
            }

            return supportedModes;
        }

        /// <summary>
        /// Connection string to be used by the transport.
        /// </summary>
        public string ConnectionString { get; internal set; }

        /// <summary>
        /// Connection string factory.
        /// </summary>
        public Func<CancellationToken, Task<SqlConnection>> ConnectionFactory { get; internal set; }

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

        static TransportTransactionMode DefaultTransactionMode => OperatingSystem.IsWindows() ? TransportTransactionMode.TransactionScope : TransportTransactionMode.SendsAtomicWithReceive;

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