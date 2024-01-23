namespace NServiceBus.Transport.PostgreSql;

using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
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
    DelayedMessageStore delayedMessageStore;
    DbConnectionFactory connectionFactory;
    ConnectionAttributes connectionAttributes;

    public PostgreSqlTransportInfrastructure(PostgreSqlTransport transport, HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses)
    {
        this.transport = transport;
        this.hostSettings = hostSettings;
        this.receivers = receivers;
        this.sendingAddresses = sendingAddresses;
    }

    public override Task Shutdown(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public override string ToTransportAddress(QueueAddress address) => throw new NotImplementedException();

    public async Task Initialize(CancellationToken cancellationToken = new())
    {
        connectionFactory = new DbConnectionFactory(async ct =>
        {
            var connection = new NpgsqlConnection(transport.ConnectionString);

            await connection.OpenAsync(ct).ConfigureAwait(false);

            return connection;
        });

        connectionAttributes = ConnectionAttributesParser.Parse(transport.ConnectionString, transport.DefaultCatalog);

        addressTranslator = new PostgreSqlQueueAddressTranslator();
        //TODO: check if we can provide streaming capability with PostgreSql
        tableBasedQueueCache = new TableBasedQueueCache(addressTranslator, false);

        delayedMessageStore = new DelayedMessageStore();

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
        throw new NotImplementedException();
    }

    async Task ConfigureSubscriptions(CancellationToken cancellationToken)
    {
        subscriptionStore = new SubscriptionStore();

        if (hostSettings.SetupInfrastructure)
        {
            await new SubscriptionTableCreator(null, null).CreateIfNecessary(cancellationToken).ConfigureAwait(false);
        }

        var pubSubSettings = transport.Subscriptions;
        var subscriptionStoreSchema = string.IsNullOrWhiteSpace(transport.DefaultSchema) ? "public" : transport.DefaultSchema;
        var subscriptionTableName = pubSubSettings.SubscriptionTableName.Qualify(subscriptionStoreSchema, connectionAttributes.Catalog);

        subscriptionStore = new PolymorphicSubscriptionStore(new SubscriptionTable(subscriptionTableName.QuotedQualifiedName, connectionFactory));

        if (pubSubSettings.DisableCaching == false)
        {
            subscriptionStore = new CachedSubscriptionStore(subscriptionStore, pubSubSettings.CacheInvalidationPeriod);
        }

        if (hostSettings.SetupInfrastructure)
        {
            await new SubscriptionTableCreator(subscriptionTableName, connectionFactory).CreateIfNecessary(cancellationToken).ConfigureAwait(false);
        }

        //transport.Testing.SubscriptionTable = subscriptionTableName.QuotedQualifiedName;
    }
}