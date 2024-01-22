namespace NServiceBus.Transport.PostgreSql;

using System.Threading;
using System.Threading.Tasks;
using SqlServer;
using QueueAddress = Transport.QueueAddress;

class PostgreSqlTransportInfrastructure : TransportInfrastructure
{
    readonly PostgreSqlTransport transport;
    readonly HostSettings hostSettings;
    readonly ReceiveSettings[] receivers;
    readonly string[] sendingAddresses;

    public PostgreSqlTransportInfrastructure(PostgreSqlTransport transport, HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses)
    {
        this.transport = transport;
        this.hostSettings = hostSettings;
        this.receivers = receivers;
        this.sendingAddresses = sendingAddresses;
    }

    public override Task Shutdown(CancellationToken cancellationToken = default) => throw new System.NotImplementedException();

    public override string ToTransportAddress(QueueAddress address) => throw new System.NotImplementedException();

    public async Task Initialize(CancellationToken cancellationToken)
    {
        var connectionString = transport.ConnectionString;

        //connectionAttributes = ConnectionAttributesParser.Parse(connectionString, transport.DefaultCatalog);

        addressTranslator = new PostgreSqlQueueAddressTranslator();
        //addressTranslator = new QueueAddressTranslator(connectionAttributes.Catalog, "dbo", transport.DefaultSchema, transport.SchemaAndCatalog);
        tableBasedQueueCache = new TableBasedQueueCache(addressTranslator, !connectionAttributes.IsEncrypted);

        await ConfigureSubscriptions(cancellationToken).ConfigureAwait(false);

        await ConfigureReceiveInfrastructure(cancellationToken).ConfigureAwait(false);

        ConfigureSendInfrastructure();
    }

    void ConfigureSendInfrastructure()
    {
        Dispatcher = new MessageDispatcher(
            addressTranslator,
            new MulticastToUnicastConverter(subscriptionStore),
            tableBasedQueueCache,
            delayedMessageStore,
            connectionFactory);
    }

    Task ConfigureReceiveInfrastructure(CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }

    Task ConfigureSubscriptions(CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }
}