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
        /// <param name="connectionFactory">Connection factory that returns an instance of <see cref="SqlConnection"/> in an Opened state.</param>
        public SqlServerTransport(Func<CancellationToken, Task<SqlConnection>> connectionFactory)
            : base(TransportTransactionMode.TransactionScope, true, true, true)
        {
            Guard.AgainstNull(nameof(connectionFactory), connectionFactory);

            ConnectionFactory = connectionFactory;
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

            if (!parser.TryGetValue("Initial Catalog", out var catalogSetting) && !parser.TryGetValue("database", out catalogSetting))
            {
                throw new Exception("Initial Catalog property is mandatory in the connection string.");
            }
            Catalog = (string)catalogSetting;

            if (parser.TryGetValue("Column Encryption Setting", out var enabled))
            {
                IsEncrypted = ((string)enabled).Equals("enabled", StringComparison.InvariantCultureIgnoreCase);
            }

            addressTranslator = new QueueAddressTranslator(Catalog, "dbo", DefaultSchema, SchemaAndCatalog);
        }

        DbConnectionStringBuilder GetConnectionStringBuilder()
        {
            if (ConnectionFactory != null)
            {
                // TODO: CRUFT & CODE SMELLS
                // SqlT is configured via this ConnectionFactory Func, but at startup time this needs to be
                // executed once to get a connection string builder so that we can discover the catalog (database)
                // name, and whether or not encryption is activated. As this is network I/O and is used at runtime,
                // it should be async and accept a cancellation token. However, the catalog name is ALSO required
                // by the ToTransportAddress method, which Core assumes should be static/deterministic. As a result,
                // ToTransportAddress is called by Core before Initialize is called, which seems like a code smell.
                // That means we have to call it here with .GetAwaiter.GetResult() and pass CancellationToken.None,
                // for code smells #2 and #3. I tried making this part of the Initialize method, which would be
                // async and provide a cancellation token, and throwing if ToTransportAddress is called before
                // Initialize, but since Core is calling ToTransportAddress before Initialize, it causes every
                // single acceptance test to fail. I'm sure we got into this situation because initiallly, we only
                // accepted a connection string, which was easy to parse. But supporting both System.Data.SqlClient
                // and Microsoft.Data.SqlClient led to us only accepting a ConnectionFactory func, but without
                // CancellationToken in the API it didn't seem that bad. Now I'm adding CancellationToken support,
                // which makes the problem obvious. At the moment I can't do a larger refactor to make this problem
                // go away, but I did at least rewrite it so that the ConnectionFactory is only called ONCE at startup
                // rather than once for each property. However, it will still be possible for a questionable connection
                // to a SQL Server at startup to cause an endpoint to violate the SLA defined by the cancellation token
                // passed to Endpoint.Start() becuase the code here won't be able to make a timely exit.
                using (var connection = ConnectionFactory(CancellationToken.None).GetAwaiter().GetResult())
                {
                    return new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
                }
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
        public Func<CancellationToken, Task<SqlConnection>> ConnectionFactory { get; internal set; }

        /// <summary>
        /// Default address schema.
        /// </summary>
        public string DefaultSchema { get; set; } = string.Empty;

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