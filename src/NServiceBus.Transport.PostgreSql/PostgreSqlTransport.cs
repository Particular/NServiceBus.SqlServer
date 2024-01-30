namespace NServiceBus.Transport.PostgreSql;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport.SqlServer;

/// <summary>
/// PostgreSql Transport
/// </summary>
public class PostgreSqlTransport : TransportDefinition
{
    /// <summary>
    /// Creates and instance of <see cref="PostgreSqlTransport"/>
    /// </summary>
    public PostgreSqlTransport(string connectionString) :base(TransportTransactionMode.TransactionScope, true, true, true)
    {
        Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

        ConnectionString = connectionString;
    }
    /// <summary>
    /// Creates and instance of <see cref="PostgreSqlTransport"/>
    /// </summary>
    public PostgreSqlTransport(TransportTransactionMode defaultTransactionMode, bool supportsDelayedDelivery, bool supportsPublishSubscribe, bool supportsTtbr)
        : base(defaultTransactionMode, supportsDelayedDelivery, supportsPublishSubscribe, supportsTtbr)
    {
    }

    /// <summary>
    /// <see cref="TransportDefinition.Initialize"/>
    /// </summary>
    public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers,
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
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new Exception("PostgreSql transport requires a connection string.");
        }
    }

    /// <summary>
    /// <see cref="TransportDefinition.GetSupportedTransactionModes"/>
    /// </summary>
    public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() => new[]{
        TransportTransactionMode.None,
        TransportTransactionMode.ReceiveOnly,
        TransportTransactionMode.SendsAtomicWithReceive,
        TransportTransactionMode.TransactionScope
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
    /// Default address catalog.
    /// </summary>
    public string DefaultCatalog { get; set; }

    /// <summary>
    /// Delayed delivery infrastructure configuration
    /// </summary>
    public DelayedDeliveryOptions DelayedDelivery { get; set; }

    /// <summary>
    /// Subscription infrastructure settings.
    /// </summary>
    public SubscriptionOptions SubscriptionOptions { get; set; }

    SqlServerTransport.TestingInformation TestingInformation { get; set; }

    //static TransportTransactionMode DefaultTransportTransactionMode = TransportTransactionMode.TransactionScope;
}