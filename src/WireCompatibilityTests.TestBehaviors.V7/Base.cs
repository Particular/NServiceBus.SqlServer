using System.Threading.Tasks;
using System.Threading;
using NServiceBus;

abstract class Base
{
    readonly string endpointName;

    protected Base(string endpointName)
    {
        this.endpointName = endpointName;
    }

    public EndpointConfiguration Configure(PluginOptions opts)
    {
        var config = new EndpointConfiguration(opts.ApplyUniqueRunPrefix(endpointName));
        config.EnableInstallers();
        config.PurgeOnStartup(true);

        var transport = new SqlServerTransport(opts.ConnectionString + $";App={endpointName}")
        {
            TransportTransactionMode = TransportTransactionMode.ReceiveOnly,
        };

        transport.Subscriptions.SubscriptionTableName = new NServiceBus.Transport.SqlServer.SubscriptionTableName(opts.ApplyUniqueRunPrefix("SubscriptionRouting"));

        var routingConfig = config.UseTransport(transport);
        config.SendFailedMessagesTo(opts.ApplyUniqueRunPrefix("error"));
        config.AuditProcessedMessagesTo(opts.AuditQueue);
        config.AddHeaderToAllOutgoingMessages(nameof(opts.TestRunId), opts.TestRunId);
        config.Pipeline.Register(new DiscardBehavior(opts.TestRunId), nameof(DiscardBehavior));

        Configure(opts, config, transport, routingConfig);

        return config;
    }

    protected virtual void Configure(
        PluginOptions opts,
        EndpointConfiguration endpointConfig,
        SqlServerTransport transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig
        )
    {
    }

    public virtual Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}