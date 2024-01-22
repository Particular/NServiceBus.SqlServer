namespace NServiceBus.Transport.PostgreSql;

using System.Threading;
using System.Threading.Tasks;
using SqlServer;
using QueueAddress = QueueAddress;

class PostgreSqlTransportInfrastructure : TransportInfrastructure
{
#pragma warning disable IDE0052
    readonly PostgreSqlTransport transport;
    readonly HostSettings hostSettings;
    readonly ReceiveSettings[] receivers;
    readonly string[] sendingAddresses;
#pragma warning restore IDE0052
    PostgreSqlQueueAddressTranslator addressTranslator;
    TableBasedQueueCache tableBasedQueueCache;
    ISubscriptionStore subscriptionStore;

    public PostgreSqlTransportInfrastructure(PostgreSqlTransport transport, HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses)
    {
        this.transport = transport;
        this.hostSettings = hostSettings;
        this.receivers = receivers;
        this.sendingAddresses = sendingAddresses;
    }

    public override Task Shutdown(CancellationToken cancellationToken = default) => throw new System.NotImplementedException();

    public override string ToTransportAddress(QueueAddress address) => throw new System.NotImplementedException();

    public async Task Initialize(CancellationToken cancellationToken = new())
    {
        //var connectionString = transport.ConnectionString;
        //connectionAttributes = ConnectionAttributesParser.Parse(connectionString, transport.DefaultCatalog);

        addressTranslator = new PostgreSqlQueueAddressTranslator();
        //TODO: check if we can provide streaming capability with PostgreSql
        tableBasedQueueCache = new TableBasedQueueCache(addressTranslator, false);

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
            null, //delayedMessageStore,
            null);//connectionFactory);
    }

    Task ConfigureReceiveInfrastructure(CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }

    async Task ConfigureSubscriptions(CancellationToken cancellationToken)
    {
        subscriptionStore = new SubscriptionStore();

        if (hostSettings.SetupInfrastructure)
        {
            await new SubscriptionTableCreator(null, null).CreateIfNecessary(cancellationToken).ConfigureAwait(false);
        }

        //transport.Testing.SubscriptionTable = subscriptionTableName.QuotedQualifiedName;
    }
}