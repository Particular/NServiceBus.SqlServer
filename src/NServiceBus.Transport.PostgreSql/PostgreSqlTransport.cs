namespace NServiceBus.Transport.PostgreSql;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Sql.Shared.Queuing;

/// <summary>
/// PostgreSql Transport
/// </summary>
public class PostgreSqlTransport : TransportDefinition
{
    /// <summary>
    /// Creates and instance of <see cref="PostgreSqlTransport"/>
    /// </summary>
    public PostgreSqlTransport(string connectionString) : this(connectionString,
        TransportTransactionMode.SendsAtomicWithReceive, true, true,
        true)
    {
    }

    /// <summary>
    /// Creates and instance of <see cref="PostgreSqlTransport"/>
    /// </summary>
    /// <param name="connectionFactory">Connection factory that returns an instance of <see cref="NpgsqlConnection"/> in an Opened state.</param>
    public PostgreSqlTransport(Func<CancellationToken, Task<NpgsqlConnection>> connectionFactory)
        : base(DefaultTransportTransactionMode, true, true, true)
    {
        Guard.AgainstNull(nameof(connectionFactory), connectionFactory);

        ConnectionFactory = connectionFactory;
    }

    /// <summary>
    /// Creates and instance of <see cref="PostgreSqlTransport"/>
    /// </summary>
    internal PostgreSqlTransport(string connectionString, TransportTransactionMode transactionMode,
        bool supportsDelayedDelivery,
        bool supportsPublishSubscribe, bool supportsTtbr)
        : base(transactionMode, supportsDelayedDelivery, supportsPublishSubscribe, supportsTtbr)
    {
        Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

        ConnectionString = connectionString;
    }

    /// <summary>
    /// <see cref="TransportDefinition.Initialize"/>
    /// </summary>
    public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings,
        ReceiveSettings[] receivers,
        string[] sendingAddresses,
        CancellationToken cancellationToken = new())
    {
        ValidateConfiguration();

        var infrastructure = new PostgreSqlTransportInfrastructure(this, hostSettings, receivers, sendingAddresses);

        await infrastructure.Initialize(cancellationToken).ConfigureAwait(false);

        return infrastructure;
    }

    void ValidateConfiguration()
    {
        //This is needed due to legacy transport api support. It can be removed when the api is no longer supported.
        if (string.IsNullOrWhiteSpace(ConnectionString) && ConnectionFactory == null)
        {
            throw new Exception("PostgreSql transport requires a connection string or a ConnectionFactory.");
        }
    }

    /// <summary>
    /// <see cref="TransportDefinition.GetSupportedTransactionModes"/>
    /// </summary>
    public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() => new[]
    {
        TransportTransactionMode.None, TransportTransactionMode.ReceiveOnly,
        TransportTransactionMode.SendsAtomicWithReceive, TransportTransactionMode.TransactionScope
    };


    /// <summary>
    /// Connection string to be used by the transport.
    /// </summary>
    public string ConnectionString { get; internal set; }

    /// <summary>
    /// Subscription infrastructure settings.
    /// </summary>
    public SubscriptionOptions Subscriptions { get; } = new SubscriptionOptions();

    /// <summary>
    /// Default address schema.
    /// </summary>
    public string DefaultSchema { get; set; } = string.Empty;

    /// <summary>
    /// Connection string factory.
    /// </summary>
    public Func<CancellationToken, Task<NpgsqlConnection>> ConnectionFactory { get; internal set; }

    /// <summary>
    /// Delayed delivery infrastructure configuration
    /// </summary>
    public DelayedDeliveryOptions DelayedDelivery { get; set; } = new DelayedDeliveryOptions();

    /// <summary>
    /// Instructs the transport to create a computed column for inspecting message body contents.
    /// </summary>
    public bool CreateMessageBodyComputedColumn { get; set; } = false;

    internal TestingInformation Testing { get; } = new TestingInformation();

    /// <summary>
    /// Transaction scope settings.
    /// </summary>
    public TransactionScopeOptions TransactionScope { get; } = new TransactionScopeOptions();

    /// <summary>
    /// Queue peeker settings.
    /// </summary>
    public QueuePeekerOptions QueuePeeker { get; set; } = new QueuePeekerOptions();

    /// <summary>
    /// Time to wait before triggering the circuit breaker.
    /// </summary>
    public TimeSpan TimeToWaitBeforeTriggeringCircuitBreaker { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Disable native delayed delivery infrastructure
    /// </summary>
    ///TODO: this is for SC usage only. It should not be public
    internal bool DisableDelayedDelivery { get; set; } = false;

    /// <summary>
    /// Catalog and schema configuration for SQL Transport queues.
    /// </summary>
    public QueueSchemaOptions SchemaAndCatalog { get; } = new QueueSchemaOptions();

    internal class TestingInformation
    {
        internal Func<string, TableBasedQueue> QueueFactoryOverride { get; set; } = null;

        internal string[] ReceiveAddresses { get; set; }

        internal string[] SendingAddresses { get; set; }

        internal string DelayedDeliveryQueue { get; set; }

        internal string SubscriptionTable { get; set; }
    }

    static TransportTransactionMode DefaultTransportTransactionMode = TransportTransactionMode.TransactionScope;
}